using BTG.Domain.Entities;

namespace BTG.Application.Interfaces;

public interface INotificacionService
{
    Task EnviarSuscripcionAsync(Cliente cliente, Fondo fondo, decimal monto, CancellationToken ct);
    Task EnviarCancelacionAsync(Cliente cliente, Fondo fondo, decimal monto, CancellationToken ct);
}

