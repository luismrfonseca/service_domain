using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Api.Controllers;
using ServiceDomain.Api.Models;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;
using Xunit;

namespace ServiceDomain.Tests.Controllers
{
    public class LogisticaControllerTests
    {
        private ServiceDomainDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ServiceDomainDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ServiceDomainDbContext(options);
        }

        // =========================================================================
        // PICKING TESTS
        // =========================================================================

        [Fact]
        public async Task GetPickingPendentes_ShouldReturnOnlySynchronizedClientOrders()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var order1 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001
            };
            var order2 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "PendenteSync",
                ClienteNo = 1002
            };
            var order3 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Sincronizado",
                ClienteNo = 5001
            };

            context.Encomendas.AddRange(order1, order2, order3);
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);

            // Act
            var result = await controller.GetPickingPendentes();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var orders = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(okResult.Value);
            Assert.Single(orders);
            Assert.Equal(order1.Id, orders.First().Id);
        }

        [Fact]
        public async Task ConfirmPicking_ShouldCreateGuiaRemessaAndOutboxItem_WhenValid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var clientOrder = new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001,
                PhcStamp = "STAMP123",
                Linhas = new List<EncomendaLinha>
                {
                    new EncomendaLinha
                    {
                        Id = lineId,
                        Ref = "PROD1",
                        Designacao = "Produto 1",
                        Quantidade = 10,
                        Preco = 5.0m,
                        PhcStamp = "LINESTAMP123"
                    }
                }
            };
            context.Encomendas.Add(clientOrder);
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new PickingDto
            {
                EncomendaId = orderId,
                Linhas = new List<PickingLinhaDto>
                {
                    new PickingLinhaDto
                    {
                        LinhaId = lineId,
                        QuantidadeRecolhida = 8,
                        Lote = "LOTE1",
                        Localizacao = "LOC1"
                    }
                }
            };

            // Act
            var result = await controller.ConfirmPicking(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic value = okResult.Value!;
            Guid guiaId = value.GetType().GetProperty("GuiaId").GetValue(value);

            // Verify Delivery Note (Guia de Remessa) was created
            var dbGuia = await context.Encomendas.Include(g => g.Linhas).FirstOrDefaultAsync(g => g.Id == guiaId);
            Assert.NotNull(dbGuia);
            Assert.Equal(DocumentoTipo.GuiaRemessa, dbGuia.Tipo);
            Assert.Equal(orderId, dbGuia.ParentId);
            Assert.Equal(40.0m, dbGuia.Total); // 8 * 5.0 = 40.0
            Assert.Single(dbGuia.Linhas);
            Assert.Equal("PROD1", dbGuia.Linhas.First().Ref);
            Assert.Equal(8, dbGuia.Linhas.First().Quantidade);
            Assert.Equal("LOTE1", dbGuia.Linhas.First().Lote);
            Assert.Equal("LOC1", dbGuia.Linhas.First().Localizacao);

            // Verify original order is marked as Prepared
            var dbOrder = await context.Encomendas.FindAsync(orderId);
            Assert.Equal("Preparada", dbOrder!.Status);

            // Verify SyncOutbox item was created
            var outbox = await context.SyncOutbox.FirstOrDefaultAsync(o => o.EntityId == guiaId);
            Assert.NotNull(outbox);
            Assert.Equal("GuiaRemessa", outbox.EntityType);
            Assert.Contains("STAMP123", outbox.Payload);
            Assert.Contains("LINESTAMP123", outbox.Payload);
        }

        [Fact]
        public async Task ConfirmPicking_ShouldReturnBadRequest_WhenQuantityExceedsOriginal()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var clientOrder = new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001,
                Linhas = new List<EncomendaLinha>
                {
                    new EncomendaLinha
                    {
                        Id = lineId,
                        Ref = "PROD1",
                        Designacao = "Produto 1",
                        Quantidade = 10,
                        Preco = 5.0m
                    }
                }
            };
            context.Encomendas.Add(clientOrder);
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new PickingDto
            {
                EncomendaId = orderId,
                Linhas = new List<PickingLinhaDto>
                {
                    new PickingLinhaDto
                    {
                        LinhaId = lineId,
                        QuantidadeRecolhida = 12, // More than 10
                        Lote = "LOTE1",
                        Localizacao = "LOC1"
                    }
                }
            };

            // Act
            var result = await controller.ConfirmPicking(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // RECEPCAO TESTS
        // =========================================================================

        [Fact]
        public async Task GetRececaoPendentes_ShouldReturnOnlySynchronizedPurchaseOrders()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var order1 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Sincronizado",
                ClienteNo = 5001
            };
            var order2 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "PendenteSync",
                ClienteNo = 5002
            };
            var order3 = new Encomenda
            {
                Id = Guid.NewGuid(),
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001
            };

            context.Encomendas.AddRange(order1, order2, order3);
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);

            // Act
            var result = await controller.GetRececaoPendentes();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var orders = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(okResult.Value);
            Assert.Single(orders);
            Assert.Equal(order1.Id, orders.First().Id);
        }

        [Fact]
        public async Task ConfirmRececao_ShouldCreateGuiaRececaoUpdateStockAndOutbox_WhenValid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var purchaseOrder = new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Sincronizado",
                ClienteNo = 5001,
                PhcStamp = "STAMP555",
                Linhas = new List<EncomendaLinha>
                {
                    new EncomendaLinha
                    {
                        Id = lineId,
                        Ref = "PROD1",
                        Designacao = "Produto 1",
                        Quantidade = 20,
                        Preco = 10.0m,
                        PhcStamp = "LINESTAMP555"
                    }
                }
            };
            context.Encomendas.Add(purchaseOrder);
            
            // Seed a stock registry for reference
            context.Stocks.Add(new Stock
            {
                Ref = "PROD1",
                LoteCodigo = "LOTXX",
                Armazem = 1,
                Localizacao = "LOC_A",
                Quantidade = 5
            });

            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new RececaoDto
            {
                EncomendaId = orderId,
                Linhas = new List<RececaoLinhaDto>
                {
                    new RececaoLinhaDto
                    {
                        LinhaId = lineId,
                        QuantidadeRecebida = 15,
                        Lote = "LOTXX",
                        Localizacao = "LOC_A"
                    }
                }
            };

            // Act
            var result = await controller.ConfirmRececao(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic value = okResult.Value!;
            Guid guiaId = value.GetType().GetProperty("GuiaId").GetValue(value);

            // Verify Delivery Receipt Note (Guia de Receção) was created
            var dbGuia = await context.Encomendas.Include(g => g.Linhas).FirstOrDefaultAsync(g => g.Id == guiaId);
            Assert.NotNull(dbGuia);
            Assert.Equal(DocumentoTipo.GuiaRececao, dbGuia.Tipo);
            Assert.Equal(orderId, dbGuia.ParentId);
            Assert.Equal(150.0m, dbGuia.Total); // 15 * 10.0 = 150.0

            // Verify original order status is updated
            var dbOrder = await context.Encomendas.FindAsync(orderId);
            Assert.Equal("Recebido", dbOrder!.Status);

            // Verify local stock is incremented (5 + 15 = 20)
            var dbStock = await context.Stocks.FirstOrDefaultAsync(s => s.Ref == "PROD1" && s.LoteCodigo == "LOTXX" && s.Localizacao == "LOC_A");
            Assert.NotNull(dbStock);
            Assert.Equal(20m, dbStock.Quantidade);

            // Verify SyncOutbox item was created
            var outbox = await context.SyncOutbox.FirstOrDefaultAsync(o => o.EntityId == guiaId);
            Assert.NotNull(outbox);
            Assert.Equal("GuiaRececao", outbox.EntityType);
        }

        [Fact]
        public async Task ConfirmPicking_ShouldReturnNotFound_WhenOrderDoesNotExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new LogisticaController(context);
            var dto = new PickingDto { EncomendaId = Guid.NewGuid(), Linhas = new List<PickingLinhaDto>() };

            // Act
            var result = await controller.ConfirmPicking(dto);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ConfirmPicking_ShouldReturnBadRequest_WhenOrderIsAlreadyPrepared()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            context.Encomendas.Add(new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Preparada",
                ClienteNo = 1001
            });
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new PickingDto { EncomendaId = orderId, Linhas = new List<PickingLinhaDto>() };

            // Act
            var result = await controller.ConfirmPicking(dto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("já foi preparada", badRequest.Value!.ToString());
        }

        [Fact]
        public async Task ConfirmPicking_ShouldReturnBadRequest_WhenLineDoesNotExistInOrder()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            context.Encomendas.Add(new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001
            });
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new PickingDto
            {
                EncomendaId = orderId,
                Linhas = new List<PickingLinhaDto>
                {
                    new PickingLinhaDto { LinhaId = Guid.NewGuid(), QuantidadeRecolhida = 1 }
                }
            };

            // Act
            var result = await controller.ConfirmPicking(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ConfirmRececao_ShouldReturnNotFound_WhenOrderDoesNotExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new LogisticaController(context);
            var dto = new RececaoDto { EncomendaId = Guid.NewGuid(), Linhas = new List<RececaoLinhaDto>() };

            // Act
            var result = await controller.ConfirmRececao(dto);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ConfirmRececao_ShouldReturnBadRequest_WhenOrderIsAlreadyReceived()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            context.Encomendas.Add(new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Recebido",
                ClienteNo = 5001
            });
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new RececaoDto { EncomendaId = orderId, Linhas = new List<RececaoLinhaDto>() };

            // Act
            var result = await controller.ConfirmRececao(dto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("já foi recebida", badRequest.Value!.ToString());
        }

        [Fact]
        public async Task ConfirmRececao_ShouldReturnBadRequest_WhenLineDoesNotExistInOrder()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();
            context.Encomendas.Add(new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Sincronizado",
                ClienteNo = 5001
            });
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);
            var dto = new RececaoDto
            {
                EncomendaId = orderId,
                Linhas = new List<RececaoLinhaDto>
                {
                    new RececaoLinhaDto { LinhaId = Guid.NewGuid(), QuantidadeRecebida = 1 }
                }
            };

            // Act
            var result = await controller.ConfirmRececao(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
