using System.Security.Claims;
using BTG.Application.DTOs;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BTG.Api.Endpoints;

public static class FondosEndpoints
{
    public static IEndpointRouteBuilder MapFondosEndpoints(this IEndpointRouteBuilder app)
    {
        // Listar fondos 
        app.MapGet("/api/fondos", [Authorize(Roles = "admin")] async (IFondoRepository repo, CancellationToken ct) =>
        {
            var fondos = await repo.GetAllAsync(ct);
            return Results.Ok(fondos);
        });

        // Suscribirse (cliente autenticado)
        app.MapPost("/api/fondos/suscribirse", [Authorize(Roles = "cliente")] async (
            SuscribirseRequest request,
            HttpContext http,
            IClienteRepository clientes,
            IFondoRepository fondos,
            ITransaccionRepository transacciones,
            INotificacionService notify,
            CancellationToken ct) =>
        {
            var sub = http.User.FindFirst("sub")?.Value
                      ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var clienteId))
                return Results.Unauthorized();

            var cliente = await clientes.GetByIdAsync(clienteId, ct);
            if (cliente is null)
                return Results.NotFound(new { error = "Cliente no encontrado (primero crea tu perfil)" });

            var fondo = await fondos.GetByIdAsync(request.FondoId, ct);
            if (fondo is null)
                return Results.NotFound(new { error = "Fondo no existe" });

            var monto = request.Monto ?? fondo.MontoMinimo;

            if (monto < fondo.MontoMinimo)
                return Results.BadRequest(new { error = $"Monto mínimo: {fondo.MontoMinimo}" });

            if (cliente.Saldo < monto)
                return Results.BadRequest(new { error = $"No tiene saldo disponible para vincularse al fondo {fondo.Nombre}" });

            if (cliente.FondosActivos.Any(x => x.FondoId == fondo.Id))
                return Results.BadRequest(new { error = $"Ya tiene suscripción activa al fondo {fondo.Nombre}" });

            // Actualizar cliente
            cliente.Saldo -= monto;
            cliente.FondosActivos.Add(new FondoActivo
            {
                FondoId = fondo.Id,
                Nombre = fondo.Nombre,
                Monto = monto,
                FechaVinculacion = DateTime.UtcNow
            });
            await clientes.UpdateAsync(cliente, ct);

            // Registrar transacción
            var tx = new Transaccion
            {
                Id = Guid.NewGuid(),
                ClienteId = cliente.Id,
                FondoId = fondo.Id,
                Tipo = "SUSCRIPCION",
                Monto = monto,
                Fecha = DateTime.UtcNow
            };
            await transacciones.AddAsync(tx, ct);

            // Notificación (mock)
            await notify.EnviarSuscripcionAsync(cliente, fondo, monto, ct);

            return Results.Ok(new { message = "Suscripción exitosa", transaccionId = tx.Id });
        });

        // Cancelar
        app.MapPost("/api/fondos/cancelar", [Authorize(Roles = "cliente")] async (
            CancelarRequest request,
            HttpContext http,
            IClienteRepository clientes,
            ITransaccionRepository transacciones,
            INotificacionService notify,
            CancellationToken ct) =>
        {
            var sub = http.User.FindFirst("sub")?.Value
                      ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var clienteId))
                return Results.Unauthorized();

            var cliente = await clientes.GetByIdAsync(clienteId, ct)
                          ?? (Cliente?)null;
            if (cliente is null)
                return Results.NotFound(new { error = "Cliente no encontrado" });

            var activo = cliente.FondosActivos.FirstOrDefault(x => x.FondoId == request.FondoId);
            if (activo is null)
                return Results.BadRequest(new { error = "El cliente no tiene suscripción activa a ese fondo" });

            // Reintegrar saldo y remover
            cliente.Saldo += activo.Monto;
            cliente.FondosActivos.Remove(activo);
            await clientes.UpdateAsync(cliente, ct);

            var tx = new Transaccion
            {
                Id = Guid.NewGuid(),
                ClienteId = cliente.Id,
                FondoId = activo.FondoId,
                Tipo = "CANCELACION",
                Monto = -activo.Monto,
                Fecha = DateTime.UtcNow
            };
            await transacciones.AddAsync(tx, ct);

            // 🔹 Notificar cancelación (ahora con método correcto)
            await notify.EnviarCancelacionAsync(cliente, new Fondo { Id = activo.FondoId, Nombre = activo.Nombre }, activo.Monto, ct);

            return Results.Ok(new { message = "Cancelación realizada con éxito", transaccionId = tx.Id });
        });

        return app;
    }
}



