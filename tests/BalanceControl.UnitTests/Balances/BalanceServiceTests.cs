using FluentAssertions;
using FluentValidation;
using Moq;
using BalanceControl.Application.Business.Balances;
using BalanceControl.Domain.Repositories.Balances;
using BalanceControl.Domain.Services.Balances.Dtos;

namespace BalanceControl.UnitTests.Balances;

public class BalanceServiceTests
{
    [Fact]
    public async Task AdjustAsync_Should_Trim_User_And_Description_Before_Repository()
    {
        var repository = new Mock<IBalanceRepository>();
        var service = new BalanceService(repository.Object, new AdjustBalanceRequestValidator());
        var request = new AdjustBalanceRequest
        {
            UserId = " user-1 ",
            OperationId = Guid.NewGuid(),
            Amount = 10m,
            Description = " test "
        };

        repository
            .Setup(item => item.AdjustAsync(
                It.IsAny<AdjustBalanceRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BalanceAdjustmentResult.Applied(new BalanceAdjustmentResponse
            {
                UserId = "user-1",
                OperationId = request.OperationId,
                MovementId = Guid.NewGuid(),
                Amount = 10m,
                Balance = 10m,
                Applied = true,
                CreatedAt = DateTime.UtcNow
            }));

        await service.AdjustAsync(request, CancellationToken.None);

        repository.Verify(item => item.AdjustAsync(
            It.Is<AdjustBalanceRequest>(value =>
                value.UserId == "user-1" &&
                value.Description == "test"),
            It.Is<string>(hash => hash.Length == 64),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustAsync_Should_Throw_ValidationException_When_User_Is_Missing()
    {
        var repository = new Mock<IBalanceRepository>();
        var service = new BalanceService(repository.Object, new AdjustBalanceRequestValidator());
        var request = new AdjustBalanceRequest
        {
            UserId = "",
            OperationId = Guid.NewGuid(),
            Amount = 10m
        };

        Func<Task> act = () => service.AdjustAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        repository.Verify(item => item.AdjustAsync(
            It.IsAny<AdjustBalanceRequest>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
