using BTG.Application.DTOs;
using BTG.Application.Exceptions;
using BTG.Application.Interfaces;
using BTG.Application.Services;
using BTG.Domain.Entities;
using Moq;
using Xunit;

namespace BTG.Tests.Application
{
    public class FondoServiceTests
    {
        private readonly Mock<IClienteRepository> _clienteRepoMock;
        private readonly Mock<IFondoRepository> _fondoRepoMock;
        private readonly Mock<ITransaccionRepository> _transaccionRepoMock;
        private readonly Mock<INotificacionService> _notificacionServiceMock;
        private readonly FondoService _service;

        public FondoServiceTests()
        {
            _clienteRepoMock = new Mock<IClienteRepository>();
            _fondoRepoMock = new Mock<IFondoRepository>();
            _transaccionRepoMock = new Mock<ITransaccionRepository>();
            _notificacionServiceMock = new Mock<INotificacionService>();

            _service = new FondoService(
                _clienteRepoMock.Object,
                _fondoRepoMock.Object,
                _transaccionRepoMock.Object,
                _notificacionServiceMock.Object
            );
        }

        [Fact]
        public async Task SuscribirseAsync_Should_Add_FondoActivo_And_Transaccion()
        {            
            var clienteId = Guid.NewGuid();
            var fondoId = Guid.NewGuid().ToString();
            var monto = 1500m;

            var cliente = new Cliente
            {
                Id = clienteId,
                Nombre = "Juan Pérez",
                Saldo = 2000m
            };

            var fondo = new Fondo
            {
                Id = fondoId,
                Nombre = "Fondo Prueba",
                MontoMinimo = 1000m
            };

            _clienteRepoMock.Setup(r => r.GetByIdAsync(clienteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cliente);

            _fondoRepoMock.Setup(r => r.GetByIdAsync(fondoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fondo);

            var request = new SuscribirseRequest(fondoId, monto);
                        
            var result = await _service.SuscribirseAsync(clienteId, request, CancellationToken.None);
                        
            Assert.Single(cliente.FondosActivos);
            Assert.Equal(500m, cliente.Saldo);
            Assert.NotEqual(Guid.Empty, result);

            _transaccionRepoMock.Verify(r =>
                r.AddAsync(It.IsAny<Transaccion>(), It.IsAny<CancellationToken>()), Times.Once);

            _notificacionServiceMock.Verify(n =>
                n.EnviarSuscripcionAsync(cliente, fondo, monto, It.IsAny<CancellationToken>()), Times.Once);
        }


        [Fact]
        public async Task SuscribirseAsync_Should_Throw_BusinessException_When_Saldo_Insuficiente()
        {
            var clienteId = Guid.NewGuid();
            var fondoId = Guid.NewGuid().ToString();

            var cliente = new Cliente
            {
                Id = clienteId,
                Nombre = "Pedro Gómez",
                Saldo = 500m
            };

            var fondo = new Fondo
            {
                Id = fondoId,
                Nombre = "Fondo Premium",
                MontoMinimo = 1000m
            };

            _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cliente);

            _fondoRepoMock.Setup(r => r.GetByIdAsync(fondo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fondo);

            var request = new SuscribirseRequest(fondoId, Monto: 1500m);
            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                _service.SuscribirseAsync(clienteId, request, CancellationToken.None));

            // Alineado con el servicio: "Saldo insuficiente"
            Assert.Contains("No tiene saldo disponible para vincularse", ex.Message);
            Assert.Contains(fondo.Nombre, ex.Message);

            _transaccionRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaccion>(), It.IsAny<CancellationToken>()), Times.Never);
            _notificacionServiceMock.Verify(n => n.EnviarSuscripcionAsync(
                It.IsAny<Cliente>(), It.IsAny<Fondo>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        }


        [Fact]
        public async Task CancelarAsync_Should_Remove_FondoActivo_And_Add_Transaccion()
        {
            // Arrange
            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                Nombre = "Test",
                Saldo = 0,
                FondosActivos = new List<FondoActivo>
        {
            new FondoActivo
            {
                FondoId = "F1",
                Nombre = "Fondo Test",
                Monto = 1500,
                FechaVinculacion = DateTime.UtcNow
            }
        }
            };

            var fondo = new Fondo { Id = "F1", Nombre = "Fondo Test", MontoMinimo = 0 };

            var mockClientes = new Mock<IClienteRepository>();
            mockClientes.Setup(r => r.GetByIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(cliente);

            var mockFondos = new Mock<IFondoRepository>();
            var mockTxs = new Mock<ITransaccionRepository>();
            var mockNotify = new Mock<INotificacionService>();

            var service = new FondoService(mockClientes.Object, mockFondos.Object, mockTxs.Object, mockNotify.Object);

            var request = new CancelarRequest { FondoId = "F1" };
                        
            await service.CancelarAsync(cliente.Id, request, CancellationToken.None);
                        
            Assert.Empty(cliente.FondosActivos);
            Assert.Equal(1500, cliente.Saldo);

            mockClientes.Verify(r => r.UpdateAsync(cliente, It.IsAny<CancellationToken>()), Times.Once);
            mockTxs.Verify(r => r.AddAsync(It.Is<Transaccion>(t =>
                t.Tipo == "CANCELACION" &&
                t.Monto == -1500
            ), It.IsAny<CancellationToken>()), Times.Once);

            mockNotify.Verify(n => n.EnviarCancelacionAsync(
                It.Is<Cliente>(c => c.Id == cliente.Id),
                It.Is<Fondo>(f => f.Id == fondo.Id),
                1500,
                It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task CancelarAsync_Should_Throw_BusinessException_When_Fondo_Not_Found()
        {
            var clienteId = Guid.NewGuid();
            var fondoId = Guid.NewGuid().ToString();

            var cliente = new Cliente
            {
                Id = clienteId,
                Nombre = "Carlos López",
                Saldo = 2000m,
                FondosActivos = new List<FondoActivo>()
            };

            _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cliente);

            var request = new CancelarRequest
            {
                FondoId = fondoId
            };

            var exception = await Assert.ThrowsAsync<BusinessException>(() =>
                _service.CancelarAsync(clienteId, request, CancellationToken.None));

            Assert.Contains("no tiene suscripción activa", exception.Message);

            _transaccionRepoMock.Verify(r =>
                r.AddAsync(It.IsAny<Transaccion>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
