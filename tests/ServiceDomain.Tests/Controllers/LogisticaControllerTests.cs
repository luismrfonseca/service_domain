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

        // =========================================================================
        // WMS OPERATION TESTS
        // =========================================================================

        [Fact]
        public void DecodeGs1_ShouldParseValidBarcodeAndThrowErrorOnDay00()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new LogisticaController(context);

            var validDto = new Gs1DecodeDto { RawBarcode = "01056012345678901727031510LOTE12345\u001D370150" };
            var invalidDto = new Gs1DecodeDto { RawBarcode = "01056012345678901727030010LOTE12345\u001D370150" }; // Day is 00

            // Act
            var resultValid = controller.DecodeGs1(validDto);
            var resultInvalid = controller.DecodeGs1(invalidDto);

            // Assert
            var okValid = Assert.IsType<OkObjectResult>(resultValid);
            var resValid = Assert.IsType<Gs1ResultDto>(okValid.Value);
            Assert.Equal("05601234567890", resValid.Gtin);
            Assert.Equal("LOTE12345", resValid.Lot);
            Assert.Equal(150, resValid.Quantity);
            Assert.Null(resValid.ValidationError);

            var okInvalid = Assert.IsType<OkObjectResult>(resultInvalid);
            var resInvalid = Assert.IsType<Gs1ResultDto>(okInvalid.Value);
            Assert.NotNull(resInvalid.ValidationError);
            Assert.Contains("Data de validade não pode conter o dia 00", resInvalid.ValidationError);
        }

        [Fact]
        public async Task GetPutawaySugestao_ShouldSuggestCqOrOptimalSpaceBasedOnProductDetails()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Produtos.AddRange(
                new Produto { Ref = "PROD_CQ", RequerCq = true, ClasseAbc = 'A', PhcStamp = "ST_CQ" },
                new Produto { Ref = "PROD_A", RequerCq = false, PesoUnitarioKg = 1.0m, VolumeUnitarioM3 = 0.05m, ClasseAbc = 'A', PhcStamp = "ST_A" }
            );

            context.Localizacoes.AddRange(
                new Localizacao { Nome = "CQ-01", Zona = "CQ", Corredor = "0", Estante = "1", PhcStamp = "LOC_CQ" },
                new Localizacao { Nome = "A-01", Zona = "A", Corredor = "1", Estante = "1", MaxPesoKg = 100m, MaxVolumeM3 = 10m, PhcStamp = "LOC_A" }
            );

            await context.SaveChangesAsync();
            var controller = new LogisticaController(context);

            // Act 1: Requires CQ
            var resultCq = await controller.GetPutawaySugestao("PROD_CQ", 10);

            // Act 2: Fits optimal class A location
            var resultOptimal = await controller.GetPutawaySugestao("PROD_A", 10);

            // Assert 1
            var okCq = Assert.IsType<OkObjectResult>(resultCq);
            var resCq = Assert.IsType<PutawaySuggestionDto>(okCq.Value);
            Assert.Equal("CQ-01", resCq.LocalizacaoId);
            Assert.Equal("CQ", resCq.Zona);

            // Assert 2
            var okOpt = Assert.IsType<OkObjectResult>(resultOptimal);
            var resOpt = Assert.IsType<PutawaySuggestionDto>(okOpt.Value);
            Assert.Equal("A-01", resOpt.LocalizacaoId);
            Assert.Equal("A", resOpt.Zona);
        }

        [Fact]
        public async Task CycleCount_ShouldTriggerBlindRecountOnDiscrepancyAndApplyAjusteOnApprove()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var stockId = Guid.NewGuid();
            context.Stocks.Add(new Stock
            {
                Id = stockId,
                Ref = "PROD1",
                Quantidade = 100m,
                Localizacao = "LOC1",
                PhcStamp = "STK_STAMP"
            });
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);

            // 1. Create Count Ordem
            var createDto = new ContagemCriarDto
            {
                TipoContagem = "ABC",
                SupervisorId = "super_01",
                StockIds = new List<Guid> { stockId }
            };
            var createResult = await controller.CriarContagem(createDto);
            var okCreate = Assert.IsType<OkObjectResult>(createResult);
            Guid orderId = (Guid)okCreate.Value.GetType().GetProperty("OrdemId").GetValue(okCreate.Value);

            var dbOrdem = await context.OrdensContagem.Include(o => o.Linhas).FirstAsync(o => o.Id == orderId);
            Guid lineId = dbOrdem.Linhas.First().Id;

            // 2. Register first count with discrepancy (System is 100, Operator counted 80)
            var regDto1 = new ContagemRegistarDto
            {
                OrdemId = orderId,
                LinhaId = lineId,
                QuantidadeContada = 80m,
                OperadorId = "op_01"
            };
            var regResult1 = await controller.RegistarContagem(regDto1);
            Assert.IsType<OkObjectResult>(regResult1);

            // Asserts order is now in recount state
            var dbOrdemRecount = await context.OrdensContagem.FindAsync(orderId);
            Assert.Equal("EM_RECONTAGEM", dbOrdemRecount.Estado);

            // 3. Register second count with same operator (should fail)
            var regDtoSameOp = new ContagemRegistarDto
            {
                OrdemId = orderId,
                LinhaId = lineId,
                QuantidadeContada = 80m,
                OperadorId = "op_01"
            };
            var regResultSame = await controller.RegistarContagem(regDtoSameOp);
            Assert.IsType<BadRequestObjectResult>(regResultSame);

            // 4. Register second count with independent operator (Operators agrees on 80)
            var regDto2 = new ContagemRegistarDto
            {
                OrdemId = orderId,
                LinhaId = lineId,
                QuantidadeContada = 80m,
                OperadorId = "op_02"
            };
            var regResult2 = await controller.RegistarContagem(regDto2);
            Assert.IsType<OkObjectResult>(regResult2);

            // 5. Approve Ordem
            var approveDto = new ContagemAprovarDto
            {
                OrdemId = orderId,
                SupervisorId = "super_01"
            };
            var approveResult = await controller.AprovarContagem(approveDto);
            Assert.IsType<OkObjectResult>(approveResult);

            // Assert final Stock is adjusted to 80
            var finalStock = await context.Stocks.FindAsync(stockId);
            Assert.Equal(80m, finalStock.Quantidade);
            Assert.NotNull(finalStock.DataUltimaContagem);

            // Assert Outbox item is generated
            var outbox = await context.SyncOutbox.FirstOrDefaultAsync(o => o.EntityId == stockId && o.EntityType == "StockAjuste");
            Assert.NotNull(outbox);
        }

        [Fact]
        public async Task GetPickingOptimized_ShouldSortLinesByCorridorAndEstanteInSShape()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var orderId = Guid.NewGuid();

            context.Localizacoes.AddRange(
                new Localizacao { Nome = "LOC-1", Corredor = "1", Estante = "5", PhcStamp = "L1" }, // Odd ascending -> Shelf 5
                new Localizacao { Nome = "LOC-2", Corredor = "1", Estante = "10", PhcStamp = "L2" }, // Odd ascending -> Shelf 10
                new Localizacao { Nome = "LOC-3", Corredor = "2", Estante = "5", PhcStamp = "L3" }, // Even descending -> Shelf 5
                new Localizacao { Nome = "LOC-4", Corredor = "2", Estante = "10", PhcStamp = "L4" } // Even descending -> Shelf 10
            );

            context.Encomendas.Add(new Encomenda
            {
                Id = orderId,
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 1001,
                Linhas = new List<EncomendaLinha>
                {
                    new EncomendaLinha { Ref = "P4", Localizacao = "LOC-4", Quantidade = 1, Designacao = "P4" },
                    new EncomendaLinha { Ref = "P2", Localizacao = "LOC-2", Quantidade = 1, Designacao = "P2" },
                    new EncomendaLinha { Ref = "P3", Localizacao = "LOC-3", Quantidade = 1, Designacao = "P3" },
                    new EncomendaLinha { Ref = "P1", Localizacao = "LOC-1", Quantidade = 1, Designacao = "P1" }
                }
            });

            await context.SaveChangesAsync();
            var controller = new LogisticaController(context);

            // Act
            var result = await controller.GetPickingOptimized(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var sorted = Assert.IsAssignableFrom<List<EncomendaLinha>>(okResult.Value);

            // Expect S-Shape sort order:
            // Corredor 1 (Odd): Ascending shelf -> LOC-1 (5) then LOC-2 (10) (Refs: P1, P2)
            // Corredor 2 (Even): Descending shelf -> LOC-4 (10) then LOC-3 (5) (Refs: P4, P3)
            Assert.Equal("P1", sorted[0].Ref);
            Assert.Equal("P2", sorted[1].Ref);
            Assert.Equal("P4", sorted[2].Ref);
            Assert.Equal("P3", sorted[3].Ref);
        }

        [Fact]
        public void ValidatePackingWeight_ShouldPassOrFailBasedOnTolerance()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new LogisticaController(context);

            var items = new List<PackedItemDto>
            {
                new PackedItemDto { Ref = "P1", Quantidade = 10, PesoUnitarioKg = 1.0m } // 10 Kg theoretical
            };

            var validDto = new ValidatePackingWeightDto
            {
                BoxTare = 0.5m, // Total theoretical = 10.5 Kg
                PackedItems = items,
                ActualWeight = 10.6m, // Within 2% of 10.5 (deviation 0.1 Kg)
                TolerancePercent = 2.0m
            };

            var invalidDto = new ValidatePackingWeightDto
            {
                BoxTare = 0.5m, // Total theoretical = 10.5 Kg
                PackedItems = items,
                ActualWeight = 12.0m, // Way over tolerance
                TolerancePercent = 2.0m
            };

            // Act
            var resValid = controller.ValidatePackingWeight(validDto);
            var resInvalid = controller.ValidatePackingWeight(invalidDto);

            // Assert
            var okValid = Assert.IsType<OkObjectResult>(resValid);
            dynamic valValid = okValid.Value;
            Assert.True((bool)valValid.GetType().GetProperty("IsValid").GetValue(valValid));

            var okInvalid = Assert.IsType<OkObjectResult>(resInvalid);
            dynamic valInvalid = okInvalid.Value;
            Assert.False((bool)valInvalid.GetType().GetProperty("IsValid").GetValue(valInvalid));
        }

        [Fact]
        public async Task ReverseLogisticsRma_ShouldCompleteAndRouteGradedItems()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var rmaId = Guid.NewGuid();
            var lineId = Guid.NewGuid();

            var rma = new Rma
            {
                Id = rmaId,
                RmaCodigo = "RMA-2026-90451",
                InvoiceRef = "FT 2026/8940",
                ClienteNo = 509123456,
                Status = "Iniciada",
                Linhas = new List<RmaLinha>
                {
                    new RmaLinha { Id = lineId, Ref = "PROD1", Quantidade = 3 }
                }
            };
            context.Rmas.Add(rma);
            await context.SaveChangesAsync();

            var controller = new LogisticaController(context);

            // 1. Grade line to 'B' (VAS)
            var gradeDto = new RmaGradeDto
            {
                RmaId = rmaId,
                LinhaId = lineId,
                Grading = "B"
            };

            var gradeRes = await controller.GradeRmaLine(gradeDto);
            var okGrade = Assert.IsType<OkObjectResult>(gradeRes);
            Assert.Equal("ZONA-VAS", okGrade.Value.GetType().GetProperty("Destino").GetValue(okGrade.Value));

            // Verify physical stock is updated
            var dbStock = await context.Stocks.FirstOrDefaultAsync(s => s.Ref == "PROD1" && s.Localizacao == "ZONA-VAS");
            Assert.NotNull(dbStock);
            Assert.Equal(3m, dbStock.Quantidade);

            // Verify RMA status is updated to 'Inspecionada'
            var dbRma = await context.Rmas.FindAsync(rmaId);
            Assert.Equal("Inspecionada", dbRma.Status);

            // 2. Settle RMA and trigger webhook
            var settleDto = new RmaSettleDto { RmaId = rmaId };
            var settleRes = await controller.SettleRma(settleDto);
            var okSettle = Assert.IsType<OkObjectResult>(settleRes);

            // Verify RMA status is Concluida
            var finalRma = await context.Rmas.FindAsync(rmaId);
            Assert.Equal("Concluida", finalRma.Status);
        }
    }
}
