using System.Text;
using CurriculosProIA.Api.Authorization;
using CurriculosProIA.App.DependencyInjection;
using CurriculosProIA.Repository.DependencyInjection;
using CurriculosProIA.Service.DependencyInjection;
using CurriculosProIA.Api.Hubs;
using CurriculosProIA.Api.Infrastructure;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// IIS/Plesk: ContentRoot = pasta do site — carrega .env (mesmo backend/.env do localhost)
EnvFileLoader.Configure(builder);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Evita 500 no swagger.json quando há rotas duplicadas (ex.: com/sem barra final).
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var allowedOrigins = new List<string>
{
    "http://localhost:4200",
    "http://127.0.0.1:4200",
    "http://localhost:58438",
    "https://curriculoproia.com.br",
    "https://www.curriculoproia.com.br",
    "https://site.curriculoproia.com.br",
    "https://www.site.curriculoproia.com.br",
    "https://curriculosproia.getpushtecnologia.com.br",
    "https://www.curriculosproia.getpushtecnologia.com.br"
};
var frontendUrl = builder.Configuration["FRONTEND_URL"]?.TrimEnd('/');
if (!string.IsNullOrEmpty(frontendUrl) && !allowedOrigins.Contains(frontendUrl))
    allowedOrigins.Add(frontendUrl);
var landingPageUrl = builder.Configuration["LANDING_PAGE_URL"]?.TrimEnd('/');
if (!string.IsNullOrEmpty(landingPageUrl) && !allowedOrigins.Contains(landingPageUrl))
    allowedOrigins.Add(landingPageUrl);

static bool IsAllowedCorsOrigin(string? origin, IReadOnlyList<string> origins)
{
    if (string.IsNullOrWhiteSpace(origin))
        return false;

    var normalized = origin.TrimEnd('/');
    if (origins.Any(o => string.Equals(o.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase)))
        return true;

    return System.Text.RegularExpressions.Regex.IsMatch(
               normalized,
               @"^https://([a-z0-9-]+\.)*curriculoproia\.com\.br$",
               System.Text.RegularExpressions.RegexOptions.IgnoreCase)
           || System.Text.RegularExpressions.Regex.IsMatch(
               normalized,
               @"^https://([a-z0-9-]+\.)*getpushtecnologia\.com\.br$",
               System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => IsAllowedCorsOrigin(origin, allowedOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition")
            .SetPreflightMaxAge(TimeSpan.FromHours(2));
    });
});

builder.Services.AddRepositories();
builder.Services.AddInfrastructureServices();
builder.Services.AddApplicationServices();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IPaymentRealtimeNotifier, PaymentRealtimeNotifier>();

var jwtSecret = builder.Configuration["JWT_SECRET"]?.Trim()
    ?? "seu_secret_key_super_seguro_aqui_mude_em_producao";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/payment", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"]?.Trim();
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]?.Trim();
if (!string.IsNullOrEmpty(googleClientId) &&
    !string.IsNullOrEmpty(googleClientSecret) &&
    !googleClientId.Contains("seu-google-client-id", StringComparison.OrdinalIgnoreCase))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/api/auth/google/callback";
    });
}

builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.Requirements.Add(new AdminRequirement()));
});

var app = builder.Build();

var enableSwagger = app.Environment.IsDevelopment()
    || string.Equals(app.Configuration["ENABLE_SWAGGER"], "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(app.Configuration["ENABLE_SWAGGER"], "1", StringComparison.OrdinalIgnoreCase);

app.UseForwardedHeaders();
app.UseRouting();
app.UseCors();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CurriculosPro IA API");
        options.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => enableSwagger
    ? Results.Redirect("/swagger")
    : Results.Json(new { status = "ok", docs = "/api/health" }));

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/analyze/payment/webhook", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/analyze/payment/mercadopago/webhook", StringComparison.OrdinalIgnoreCase))
        context.Request.EnableBuffering();

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PaymentHub>("/hubs/payment");

app.Run();
