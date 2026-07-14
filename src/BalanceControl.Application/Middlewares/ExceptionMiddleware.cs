using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BalanceControl.Domain.Exceptions;

namespace BalanceControl.Application.Middlewares;

public class ExceptionMiddleware(
    RequestDelegate next,
    IHostEnvironment env,
    IConfiguration configuration,
    ILogger<ExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly IHostEnvironment _env = env;
    private readonly ApiErrorDetailOptions _options = ApiErrorDetailOptions.Parse(configuration, env);
    private readonly ILogger<ExceptionMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var error = MapException(ex);
            using var databaseErrorScope = BeginDatabaseErrorScope(ex);

            if (error.StatusCode >= (int)HttpStatusCode.InternalServerError)
            {
                if (error.IsDefault)
                {
                    _logger.LogError(
                        ex,
                        "Unhandled exception used default error mapping while processing {Method} {Path}. TraceId: {TraceId}. Add explicit handling if this case is expected.",
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);
                }
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Handled request exception as {StatusCode} while processing {Method} {Path}. TraceId: {TraceId}",
                    error.StatusCode,
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);
            }

            context.Response.StatusCode = error.StatusCode;
            context.Response.ContentType = "application/json";

            var details = BuildDetails(ex, error, context.TraceIdentifier);

            var finalResponse = new
            {
                data = new { },
                errors = new[]
                {
                    new
                    {
                        code = error.StatusCode,
                        message = error.Message,
                        details
                    }
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(finalResponse));
        }
    }

    private IDisposable? BeginDatabaseErrorScope(Exception exception)
    {
        var databaseError = DatabaseExceptionLogScope.TryCreate(exception);

        return databaseError is null
            ? null
            : _logger.BeginScope(databaseError);
    }

    private object? BuildDetails(
        Exception exception,
        MappedException error,
        string traceIdentifier)
        => _options.Mode switch
        {
            ApiErrorDetailMode.LocalDevelopment => new
            {
                exception.Message,
                ExceptionType = exception.GetType().FullName,
                exception.StackTrace,
                error.ValidationErrors,
                TraceIdentifier = traceIdentifier
            },
            ApiErrorDetailMode.ReleaseTest => new
            {
                exception.Message,
                ExceptionType = exception.GetType().FullName,
                error.ValidationErrors,
                TraceIdentifier = traceIdentifier
            },
            _ => null
        };

    private static MappedException MapException(Exception exception)
        => exception switch
        {
            ValidationException validationException => new(
                StatusCodes.Status422UnprocessableEntity,
                "Erro de validação.",
                validationException.Errors
                    .Select(error => new
                    {
                        field = error.PropertyName,
                        message = error.ErrorMessage
                    })
                    .ToArray()),
            ArgumentException argumentException => new(
                StatusCodes.Status400BadRequest,
                argumentException.Message),
            KeyNotFoundException keyNotFoundException => new(
                StatusCodes.Status404NotFound,
                keyNotFoundException.Message),
            NotFoundException notFoundException => new(
                StatusCodes.Status404NotFound,
                notFoundException.Message),
            OperationCanceledException => new(
                StatusCodes.Status408RequestTimeout,
                "Request timeout."),
            BadHttpRequestException badHttpRequestException => new(
                badHttpRequestException.StatusCode,
                badHttpRequestException.Message),
            _ => new(
                StatusCodes.Status500InternalServerError,
                "Ocorreu um erro interno. Tente novamente mais tarde.",
                IsDefault: true)
        };

    private sealed record MappedException(
        int StatusCode,
        string Message,
        object? ValidationErrors = null,
        bool IsDefault = false);

    private sealed record ApiErrorDetailOptions(ApiErrorDetailMode Mode)
    {
        public static ApiErrorDetailOptions Parse(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            bool exposeDetails = configuration.GetValue("ApiErrors:ExposeDetails", false);
            string releaseChannel = FirstNotEmpty(
                configuration["RELEASE_CHANNEL"],
                configuration["OBSERVABILITY_RELEASE_CHANNEL"],
                environment.IsDevelopment() ? "development" : "release");

            if (exposeDetails)
            {
                bool allowed = IsStageTest(releaseChannel) || IsTesting(environment);
                if (!allowed || environment.IsProduction() || IsRelease(releaseChannel))
                {
                    throw new InvalidOperationException(
                        "ApiErrors:ExposeDetails so pode ser habilitado com RELEASE_CHANNEL=stage-test ou ASPNETCORE_ENVIRONMENT=Testing.");
                }

                return new ApiErrorDetailOptions(ApiErrorDetailMode.ReleaseTest);
            }

            if (environment.IsDevelopment() && !IsRelease(releaseChannel))
                return new ApiErrorDetailOptions(ApiErrorDetailMode.LocalDevelopment);

            return new ApiErrorDetailOptions(ApiErrorDetailMode.None);
        }

        private static bool IsStageTest(string releaseChannel)
            => string.Equals(releaseChannel, "stage-test", StringComparison.OrdinalIgnoreCase);

        private static bool IsRelease(string releaseChannel)
            => string.Equals(releaseChannel, "release", StringComparison.OrdinalIgnoreCase);

        private static bool IsTesting(IHostEnvironment environment)
            => string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase);

        private static string FirstNotEmpty(params string?[] values)
            => values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private enum ApiErrorDetailMode
    {
        None,
        LocalDevelopment,
        ReleaseTest
    }

    private static class DatabaseExceptionLogScope
    {
        private const string PostgresExceptionTypeName = "Npgsql.PostgresException";

        public static IReadOnlyDictionary<string, object?>? TryCreate(Exception exception)
        {
            var postgresException = FindPostgresException(exception);
            if (postgresException is null)
                return null;

            return new Dictionary<string, object?>
            {
                ["db.system"] = "postgresql",
                ["db.sql_state"] = GetPropertyValue(postgresException, "SqlState"),
                ["db.error.hint"] = GetPropertyValue(postgresException, "Hint"),
                ["db.error.severity"] = GetPropertyValue(postgresException, "Severity"),
                ["db.schema"] = GetPropertyValue(postgresException, "SchemaName"),
                ["db.table"] = GetPropertyValue(postgresException, "TableName"),
                ["db.column"] = GetPropertyValue(postgresException, "ColumnName"),
                ["db.constraint"] = GetPropertyValue(postgresException, "ConstraintName"),
                ["db.context"] = ExtractBalanceControlDatabaseFrame(postgresException)
            };
        }

        private static Exception? FindPostgresException(Exception exception)
        {
            var current = exception;
            while (current is not null)
            {
                if (current.GetType().FullName == PostgresExceptionTypeName)
                    return current;

                current = current.InnerException;
            }

            return null;
        }

        private static object? GetPropertyValue(Exception exception, string propertyName)
            => exception.GetType().GetProperty(propertyName)?.GetValue(exception);

        private static string? ExtractBalanceControlDatabaseFrame(Exception exception)
        {
            if (string.IsNullOrWhiteSpace(exception.StackTrace))
                return null;

            return exception.StackTrace
                .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(frame => frame.Contains(
                    "BalanceControl.Infrastructure.Database.",
                    StringComparison.Ordinal));
        }
    }
}
