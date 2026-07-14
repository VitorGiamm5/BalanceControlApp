using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BalanceControl.Domain.Services.Balances.Dtos;

namespace BalanceControl.Application.Business.Balances;

internal static class BalanceRequestHasher
{
    public static string Compute(AdjustBalanceRequest request)
    {
        var occurredAt = request.OccurredAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        var canonical = string.Join(
            "|",
            request.UserId.Trim(),
            request.OperationId.ToString("D"),
            request.Amount.ToString("G29", CultureInfo.InvariantCulture),
            occurredAt,
            request.Description?.Trim() ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
