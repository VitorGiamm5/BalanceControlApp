using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BalanceControl.Application.Middlewares;

public class JsonDeserializationExceptionMiddleware(
    RequestDelegate next,
    ILogger<JsonDeserializationExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<JsonDeserializationExceptionMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        // Apenas intercepta métodos que enviam body
        if (IsBodyMethod(context.Request.Method))
        {
            context.Request.EnableBuffering(); // permite reler o body
        }
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsJsonException(ex))
        {
            _logger.LogWarning(
                ex,
                "Invalid JSON payload while processing {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await HandleJsonExceptionAsync(context, ex);
        }
    }

    private static bool IsBodyMethod(string method) =>
        method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("PUT", StringComparison.OrdinalIgnoreCase);

    private static bool IsJsonException(Exception ex) =>
        ex is JsonException ||
        ex is BadHttpRequestException { Data: { }, Message: "" } ||
        (ex.InnerException is JsonException);

    private static async Task HandleJsonExceptionAsync(HttpContext context, Exception ex)
    {
        string? rawBody = null;

        try
        {
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            rawBody = await reader.ReadToEndAsync();
        }
        catch { }

        string message = ExtractMessage(ex);
        string detail = ExtractDetail(ex);

        var errorResponse = new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "Invalid JSON payload",
            status = (int)HttpStatusCode.BadRequest,
            detail,
            message,
            path = context.Request.Path.Value,
            traceId = context.TraceIdentifier
        };

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/problem+json";

        var finalResponse = new
        {
            data = new { },
            errors = errorResponse
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(finalResponse));
    }

    private static string ExtractMessage(Exception ex)
    {
        var jsonEx = ex as JsonException ?? ex.InnerException as JsonException;

        if (jsonEx is null)
            return "O corpo da requisição contém JSON malformado.";

        var msg = $"JSON inválido: {jsonEx.Message}";

        if (jsonEx.LineNumber.HasValue)
            msg += $" (linha {jsonEx.LineNumber + 1}, posição {jsonEx.BytePositionInLine})";

        return msg;
    }

    private static string ExtractDetail(Exception ex)
    {
        var jsonEx = ex as JsonException ?? ex.InnerException as JsonException;
        return jsonEx?.Path ?? ex.Message;
    }
}
