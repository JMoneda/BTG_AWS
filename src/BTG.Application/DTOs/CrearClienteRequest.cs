using BTG.Domain.Entities;

namespace BTG.Application.DTOs
{
    public record CrearClienteRequest(
        string Nombre,
        string Email,
        string Telefono,
        decimal Saldo,
        PreferenciaNotificacion PreferenciaNotificacion);
}