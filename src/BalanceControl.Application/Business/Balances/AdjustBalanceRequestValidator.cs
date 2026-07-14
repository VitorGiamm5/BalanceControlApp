using FluentValidation;
using BalanceControl.Domain.Services.Balances.Dtos;

namespace BalanceControl.Application.Business.Balances;

public sealed class AdjustBalanceRequestValidator : AbstractValidator<AdjustBalanceRequest>
{
    private const decimal MaxAmount = 9999999999999999.99m;
    private const decimal MinAmount = -9999999999999999.99m;

    public AdjustBalanceRequestValidator()
    {
        RuleFor(request => request.UserId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.OperationId)
            .NotEmpty();

        RuleFor(request => request.Amount)
            .NotEqual(0m)
            .Must(amount => amount is >= MinAmount and <= MaxAmount)
            .WithMessage("Amount must fit numeric(18,2).")
            .Must(HasAtMostTwoDecimalPlaces)
            .WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(request => request.Description)
            .MaximumLength(500);
    }

    private static bool HasAtMostTwoDecimalPlaces(decimal amount)
        => decimal.Round(amount, 2) == amount;
}
