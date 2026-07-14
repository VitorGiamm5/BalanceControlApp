using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using BalanceControl.Api.Configuration;
using BalanceControl.Api.Converters;
using BalanceControl.Api.Services.Auth;
using BalanceControl.Application;
using BalanceControl.Application.Middlewares;
using BalanceControl.Infrastructure;
using BalanceControl.Infrastructure.Database.Services;
using BalanceControl.Observability;
using BalanceControl.ServiceDefaults;
using Prometheus;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.AddServiceDefaults();

ObservabilitySettings observability = builder.AddBalanceControlObservability(
    "balance-control-api");

Log.Logger = new LoggerConfiguration()
    .ConfigureBalanceControlLogging(observability)
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .ConfigureBalanceControlLogging(observability));

builder.WebHost
    .UseUrls()
    .ConfigureKestrel(options =>
    {
        int kestrelPort = ApiProtectionOptions.GetKestrelPort(builder.Configuration);

        options.ListenAnyIP(kestrelPort);
        options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(
            ApiProtectionOptions.GetKeepAliveTimeoutSeconds(builder.Configuration));
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(
            ApiProtectionOptions.GetRequestHeadersTimeoutSeconds(builder.Configuration));
    });

builder.Services.AddControllers(mvc =>
{
    mvc.SuppressAsyncSuffixInActionNames = false;
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.DefaultBufferSize = 4096;

        options.JsonSerializerOptions.Converters.Add(new TrimStringJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = InvalidModelStateFactory.ExecuteAsync();
    });

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Balance Control API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT usando o header Authorization. Exemplo: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.OperationFilter<AuthorizeOperationFilter>();
});

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options =>
    {
        options.Validate();
        return true;
    })
    .ValidateOnStart();

JwtOptions jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtOptions.GetSigningKey(),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<SimpleJwtTokenService>();

builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration
        .GetSection(ApiCorsOptions.SectionName)
        .Get<ApiCorsOptions>() ?? new ApiCorsOptions();

    var allowedOrigins = corsOptions.GetAllowedOrigins();
    if (allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException("ApiCors:AllowedOrigins must contain at least one origin or '*'.");
    }

    options.AddPolicy(ApiCorsOptions.PolicyName, policy =>
    {
        policy.AllowAnyHeader()
            .WithMethods("GET", "POST");

        if (corsOptions.AllowsAnyOrigin())
        {
            policy.AllowAnyOrigin();
            return;
        }

        policy.WithOrigins(allowedOrigins);
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

app.UseMetricServer();
app.UseHttpMetrics();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("http.request.method", httpContext.Request.Method);
        diagnosticContext.Set(
            "http.route",
            httpContext.GetEndpoint() is RouteEndpoint routeEndpoint
                ? routeEndpoint.RoutePattern.RawText ?? "unknown"
                : "unknown");
        diagnosticContext.Set("http.response.status_code", httpContext.Response.StatusCode);
    };
});

if (builder.Configuration.GetValue("DatabaseSettings:RunMigrationsOnStartup", true))
{
    await ExecutePendingMigration.ExecuteAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    UseOpenApi(app);
}

app.UseMiddleware<JsonDeserializationExceptionMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors(ApiCorsOptions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

await app.RunAsync();

static void UseOpenApi(WebApplication app)
{
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}
