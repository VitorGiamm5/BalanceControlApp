using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BalanceControl.Api.Configuration;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var allowAnonymous = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>()
            .Any() ||
            context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any() == true;

        if (allowAnonymous)
            return;

        var requiresAuthorization = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any() ||
            context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any() == true;

        if (!requiresAuthorization)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference("Bearer", context.Document, null)
            ] = []
        });

        operation.Responses ??= [];
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(),
            new OpenApiResponse { Description = "Unauthorized" });
    }
}
