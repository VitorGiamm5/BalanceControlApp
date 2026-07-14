using System.Globalization;
using FluentAssertions;
using BalanceControl.Application.Business.Balances;
using BalanceControl.Domain.Services.Balances.Dtos;

namespace BalanceControl.UnitTests.Balances;

public class AdjustBalanceRequestValidatorTests
{
    private readonly AdjustBalanceRequestValidator _validator = new();

    [Theory]
    [InlineData("100.50")]
    [InlineData("-40.00")]
    public void Validate_Should_Accept_Positive_And_Negative_Amounts(string amount)
    {
        var request = BuildValidRequest();
        request.Amount = decimal.Parse(amount, CultureInfo.InvariantCulture);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_Should_Reject_Missing_UserId(string userId)
    {
        var request = BuildValidRequest();
        request.UserId = userId;

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AdjustBalanceRequest.UserId));
    }

    [Fact]
    public void Validate_Should_Reject_UserId_Above_Max_Length()
    {
        var request = BuildValidRequest();
        request.UserId = new string('u', 101);

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AdjustBalanceRequest.UserId));
    }

    [Fact]
    public void Validate_Should_Reject_Empty_OperationId()
    {
        var request = BuildValidRequest();
        request.OperationId = Guid.Empty;

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AdjustBalanceRequest.OperationId));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1.234")]
    [InlineData("10000000000000000.00")]
    [InlineData("-10000000000000000.00")]
    public void Validate_Should_Reject_Invalid_Amount(string amount)
    {
        var request = BuildValidRequest();
        request.Amount = decimal.Parse(amount, CultureInfo.InvariantCulture);

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AdjustBalanceRequest.Amount));
    }

    [Fact]
    public void Validate_Should_Reject_Description_Above_Max_Length()
    {
        var request = BuildValidRequest();
        request.Description = new string('d', 501);

        var result = _validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AdjustBalanceRequest.Description));
    }

    private static AdjustBalanceRequest BuildValidRequest()
        => new()
        {
            UserId = "user-0001",
            OperationId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Amount = 100.50m,
            OccurredAt = new DateTime(2026, 7, 13, 18, 0, 0, DateTimeKind.Utc),
            Description = "initial credit"
        };
}
