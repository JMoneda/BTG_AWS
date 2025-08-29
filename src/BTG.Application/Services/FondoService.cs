using BTG.Application.DTOs;
using BTG.Application.Exceptions;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;

namespace BTG.Application.Services;
public class FondoService : IFondoService
{
    private readonly IClienteRepository _clientes;
    private readonly IFondoRepository _fondos;
    private readonly ITransaccionRepository _txs;
    private readonly INotificacionService _notify;

    public FondoService(
        IClienteRepository clientes,
        IFondoRepository fondos,
        ITransaccionRepository txs,
        INotificacionService notify)
    {
        _clientes = clientes;
        _fondos = fondos;
        _txs = txs;
        _notify = notify;
    }

    public async Task<Guid> SuscribirseAsync(Guid clienteId, SuscribirseRequest req, CancellationToken ct)
    {
        var cliente = await _clientes.GetByIdAsync(clienteId, ct)
                      ?? throw new BusinessException("Cliente no encontrado", 404);

        var fondo = await _fondos.GetByIdAsync(req.FondoId, ct)
                     ?? throw new BusinessException("Fondo no existe", 404);

        if (req.Monto < fondo.MontoMinimo)
            throw new BusinessException($"Monto mínimo: {fondo.MontoMinimo}");

        if (cliente.Saldo < req.Monto)
            throw new BusinessException(
                $"No tiene saldo disponible para vincularse al fondo {fondo.Nombre}",
                400
            );

        if (req.Monto is null)
            throw new BusinessException("El monto de suscripción es obligatorio", 400);

        // Actualizar saldo y agregar fondo activo
        cliente.Saldo -= req.Monto.Value;
        cliente.FondosActivos.Add(new FondoActivo
        {
            FondoId = fondo.Id,
            Nombre = fondo.Nombre,
            Monto = req.Monto.Value,
            FechaVinculacion = DateTime.UtcNow
        });

        await _clientes.UpdateAsync(cliente, ct);

        var transaccion = new Transaccion
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            FondoId = fondo.Id,
            Tipo = "SUSCRIPCION",
            Monto = req.Monto.Value,
            Fecha = DateTime.UtcNow
        };

        await _txs.AddAsync(transaccion, ct);

        //Enviar notificación al cliente
        await _notify.EnviarSuscripcionAsync(cliente, fondo, req.Monto.Value, ct);


        return transaccion.Id;
    }

    public async Task<Guid> CancelarAsync(Guid clienteId, CancelarRequest req, CancellationToken ct)
    {
        var cliente = await _clientes.GetByIdAsync(clienteId, ct)
            ?? throw new BusinessException("Cliente no existe", 404);

        var activo = cliente.FondosActivos.FirstOrDefault(x => x.FondoId == req.FondoId)
            ?? throw new BusinessException("El cliente no tiene suscripción activa a ese fondo");

        cliente.Saldo += activo.Monto;
        cliente.FondosActivos.Remove(activo);
        await _clientes.UpdateAsync(cliente, ct);

        var tx = new Transaccion
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            FondoId = req.FondoId,
            Tipo = "CANCELACION",
            Monto = -activo.Monto,
            Fecha = DateTime.UtcNow
        };

        await _txs.AddAsync(tx, ct);

        var fondo = new Fondo { Id = activo.FondoId, Nombre = activo.Nombre, MontoMinimo = 0 };
        await _notify.EnviarCancelacionAsync(cliente, fondo, activo.Monto, ct);

        return tx.Id;
    }


    // Obtener historial de transacciones del cliente
    public Task<List<Transaccion>> HistorialAsync(Guid clienteId, CancellationToken ct)
        => _txs.GetByClienteAsync(clienteId, ct);
}
