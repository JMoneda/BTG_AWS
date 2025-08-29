using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BTG.Infrastructure.Notifications;

public class ConsoleNotificacionService : INotificacionService
{
    private readonly ILogger<ConsoleNotificacionService> _logger;

    public ConsoleNotificacionService(ILogger<ConsoleNotificacionService> logger)
    {
        _logger = logger;
    }

    public Task EnviarSuscripcionAsync(Cliente cliente, Fondo fondo, decimal monto, CancellationToken ct)
    {
        _logger.LogInformation("Notificación enviada por {Canal} -> Cliente:{Cliente} Fondo:{Fondo} Monto:{Monto}",
            cliente.PreferenciaNotificacion, cliente.Nombre, fondo.Nombre, monto);

        return Task.CompletedTask;
    }
    public Task EnviarCancelacionAsync(Cliente cliente, Fondo fondo, decimal monto, CancellationToken ct)
    {
        _logger.LogInformation("Cancelación enviada por {Canal} -> Cliente:{Cliente} Fondo:{Fondo} Monto:{Monto}",
            cliente.PreferenciaNotificacion, cliente.Nombre, fondo.Nombre, monto);

        return Task.CompletedTask;
    }


}
