using System.Text;
using CurriculosProIA.Api.Authorization;
using CurriculosProIA.App.DependencyInjection;
using CurriculosProIA.Repository.DependencyInjection;
using CurriculosProIA.Service.DependencyInjection;
using CurriculosProIA.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// DotNetEnv já definiu process env; reforça no IConfiguration
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = new List<string>
{
    "http://localhost:4200",
    "http://localhost:58438",
    "https://curriculosproia.getpushtecnologia.com.br",
    "https://www.curriculosproia.getpushtecnologia.com.br"
};
var frontendUrl = builder.Configuration["FRONTEND_URL"]?.TrimEnd('/');
if (!string.IsNullOrEmpty(frontendUrl) && !allowedOrigins.Contains(frontendUrl))
    allowedOrigins.Add(frontendUrl);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return true;
                var normalized = origin.TrimEnd('/');
                if (allowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase)))
                    return true;
                return System.Text.RegularExpressions.Regex.IsMatch(
                    origin,
                    @"^https://([a-z0-9-]+\.)*getpushtecnologia\.com\.br$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRepositories();
builder.Services.AddInfrastructureServices();
builder.Services.AddApplicationServices();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/analyze/payment/webhook", StringComparison.OrdinalIgnoreCase))
        context.Request.EnableBuffering();

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
