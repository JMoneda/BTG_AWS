using BTG.Application.Interfaces;
using BTG.Application.Services;
using BTG.Infrastructure.Repositories;
using BTG.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace BTG.Api.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration cfg)
    {
        var key = cfg.GetSection("Jwt:Key").Get<string>()
                  ?? throw new InvalidOperationException("Jwt:Key missing");

        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserRepository, UserRepositoryDynamoDb>();

        // Políticas de roles
        services.AddAuthorization(options =>
        {
            options.AddPolicy("OnlyAdmins", p => p.RequireRole("admin"));
            options.AddPolicy("OnlyClients", p => p.RequireRole("cliente"));
            options.AddPolicy("CanCancelFondo", p => p.RequireRole("admin", "cliente"));
        });

        // Configuración de autenticación JWT
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = cfg["Jwt:Issuer"],
                ValidAudience = cfg["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
