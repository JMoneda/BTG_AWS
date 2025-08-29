using Amazon.DynamoDBv2;
using BTG.Application.Interfaces;
using BTG.Application.Services;
using BTG.Infrastructure.Notifications;
using BTG.Infrastructure.Repositories;
using BTG.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTG.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // DynamoDB
            services.AddSingleton<IAmazonDynamoDB>(_ =>
            {
                var cfg = new AmazonDynamoDBConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
                return new AmazonDynamoDBClient(cfg);
            });
            services.AddScoped<IClienteRepository, ClienteRepositoryDynamoDb>();
            services.AddScoped<IFondoRepository, FondoRepositoryDynamoDb>();
            services.AddScoped<ITransaccionRepository, TransaccionRepositoryDynamoDb>();
            services.AddScoped<IUserRepository, UserRepositoryDynamoDb>();

            // Servicios de dominio
            services.AddScoped<IFondoService, FondoService>();

            // Notificaciones
            services.AddScoped<INotificacionService, ConsoleNotificacionService>();

            // Seguridad (hasher)
            services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

            return services;
        }
    }
}
