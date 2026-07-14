using Microsoft.AspNetCore.Mvc;

namespace BalanceControl.Api.Converters;

public static class InvalidModelStateFactory
{
    public static Func<ActionContext, IActionResult> ExecuteAsync()
    {
        return context =>
        {
            var isJsonError = context.ModelState
                .Any(e => e.Value?.Errors
                    .Any(x => x.ErrorMessage.Contains("JSON") ||
                              x.ErrorMessage.Contains("invalid") ||
                              x.ErrorMessage.Contains("Path: $")) == true);

            if (isJsonError)
            {
                var result = new ObjectResult(new
                {
                    data = new { },
                    errors = new
                    {
                        type = "https://tools.ietf.org/html/rfc7807",
                        title = "Invalid JSON payload",
                        status = StatusCodes.Status400BadRequest,
                        detail = "O corpo da requisição contém JSON malformado ou caracteres inválidos.",
                        messages = context.ModelState
                            .Where(e => e.Value?.Errors.Any() == true)
                            .SelectMany(e => e.Value!.Errors.Select(x => x.ErrorMessage))
                            .ToList(),
                        path = context.HttpContext.Request.Path.Value,
                        traceId = context.HttpContext.TraceIdentifier
                    }
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };

                result.ContentTypes.Add("application/problem+json");
                return result;
            }

            var validationResult = new ObjectResult(new
            {
                data = new { },
                errors = new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Validation failed",
                    status = StatusCodes.Status400BadRequest,
                    messages = context.ModelState
                        .Where(e => e.Value?.Errors.Any() == true)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
                        ),
                    path = context.HttpContext.Request.Path.Value,
                    traceId = context.HttpContext.TraceIdentifier
                }
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

            validationResult.ContentTypes.Add("application/problem+json");
            return validationResult;
        };
    }
}
