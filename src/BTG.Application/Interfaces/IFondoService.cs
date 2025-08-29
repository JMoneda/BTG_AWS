using BTG.Application.DTOs;
using BTG.Domain.Entities;

namespace BTG.Application.Interfaces
{
    public interface IFondoService
    {
        Task<Guid> SuscribirseAsync(Guid clienteId, SuscribirseRequest req, CancellationToken ct);
        Task<Guid> CancelarAsync(Guid clienteId, CancelarRequest req, CancellationToken ct);
        Task<List<Transaccion>> HistorialAsync(Guid clienteId, CancellationToken ct);
    }
}
