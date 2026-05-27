using System;
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
    public class StocksControllerTests
    {
        private ServiceDomainDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ServiceDomainDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ServiceDomainDbContext(options);
        }

        [Fact]
        public async Task GetStocks_ShouldReturnAllStocks_WithoutFilters()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Stocks.AddRange(
                new Stock { Ref = "PROD1", Armazem = 1, Localizacao = "LOC1", Quantidade = 10 },
                new Stock { Ref = "PROD2", Armazem = 1, Localizacao = "LOC2", Quantidade = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new StocksController(context);

            // Act
            var result = await controller.GetStocks(null, null, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stocks = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Stock>>(okResult.Value);
            Assert.Equal(2, stocks.Count());
        }

        [Fact]
        public async Task GetStocks_ShouldFilterByRef_WhenRefCodeIsProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Stocks.AddRange(
                new Stock { Ref = "PROD1", Armazem = 1, Localizacao = "LOC1", Quantidade = 10 },
                new Stock { Ref = "PROD2", Armazem = 1, Localizacao = "LOC2", Quantidade = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new StocksController(context);

            // Act
            var result = await controller.GetStocks("PROD1", null, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stocks = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Stock>>(okResult.Value);
            Assert.Single(stocks);
            Assert.Equal("PROD1", stocks.First().Ref);
        }

        [Fact]
        public async Task RecordMovement_ShouldCreateNewStock_IfItemDoesNotExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new StocksController(context);
            var dto = new StockMovementDto
            {
                Ref = "PROD100",
                LoteCodigo = "L100",
                Armazem = 1,
                Localizacao = "PRAT1",
                Quantidade = 50
            };

            // Act
            var result = await controller.RecordMovement(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic value = okResult.Value!;
            decimal resultQuantity = value.GetType().GetProperty("ResultQuantity").GetValue(value);
            Assert.Equal(50m, resultQuantity);

            // Verify db item exists
            var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Ref == "PROD100" && s.LoteCodigo == "L100");
            Assert.NotNull(stock);
            Assert.Equal(50m, stock.Quantidade);

            // Verify outbox item
            var outboxItem = await context.SyncOutbox.FirstOrDefaultAsync(x => x.EntityId == stock.Id);
            Assert.NotNull(outboxItem);
            Assert.Equal("StockMovimento", outboxItem.EntityType);
            Assert.Contains("PROD100", outboxItem.Payload);
        }

        [Fact]
        public async Task RecordMovement_ShouldUpdateExistingStock_IfItemExists()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var stockId = Guid.NewGuid();
            var existingStock = new Stock
            {
                Id = stockId,
                Ref = "PROD1",
                LoteCodigo = "L1",
                Armazem = 1,
                Localizacao = "LOC1",
                Quantidade = 10
            };
            context.Stocks.Add(existingStock);
            await context.SaveChangesAsync();

            var controller = new StocksController(context);
            var dto = new StockMovementDto
            {
                Ref = "PROD1",
                LoteCodigo = "L1",
                Armazem = 1,
                Localizacao = "LOC1",
                Quantidade = 5 // Add 5
            };

            // Act
            var result = await controller.RecordMovement(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic value = okResult.Value!;
            decimal resultQuantity = value.GetType().GetProperty("ResultQuantity").GetValue(value);
            Assert.Equal(15m, resultQuantity);

            var dbStock = await context.Stocks.FindAsync(stockId);
            Assert.NotNull(dbStock);
            Assert.Equal(15m, dbStock.Quantidade);
        }

        [Fact]
        public async Task RecordMovement_ShouldReturnBadRequest_IfResultingStockIsNegative()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Stocks.Add(new Stock
            {
                Ref = "PROD1",
                Armazem = 1,
                Localizacao = "LOC1",
                Quantidade = 10
            });
            await context.SaveChangesAsync();

            var controller = new StocksController(context);
            var dto = new StockMovementDto
            {
                Ref = "PROD1",
                Armazem = 1,
                Localizacao = "LOC1",
                Quantidade = -15 // Subtract 15 (results in -5)
            };

            // Act
            var result = await controller.RecordMovement(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetStocks_ShouldFilterByLoteArmazemAndLocalizacao_WhenProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Stocks.AddRange(
                new Stock { Ref = "PROD1", Armazem = 1, Localizacao = "LOC1", LoteCodigo = "LOT1", Quantidade = 10 },
                new Stock { Ref = "PROD1", Armazem = 2, Localizacao = "LOC2", LoteCodigo = "LOT2", Quantidade = 5 }
            );
            await context.SaveChangesAsync();

            var controller = new StocksController(context);

            // Act & Assert 1: filter by loteCodigo
            var res1 = await controller.GetStocks(null, "LOT1", null, null);
            var ok1 = Assert.IsType<OkObjectResult>(res1);
            var stocks1 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Stock>>(ok1.Value);
            Assert.Single(stocks1);
            Assert.Equal("LOT1", stocks1.First().LoteCodigo);

            // Act & Assert 2: filter by armazem
            var res2 = await controller.GetStocks(null, null, 2, null);
            var ok2 = Assert.IsType<OkObjectResult>(res2);
            var stocks2 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Stock>>(ok2.Value);
            Assert.Single(stocks2);
            Assert.Equal(2, stocks2.First().Armazem);

            // Act & Assert 3: filter by localizacao
            var res3 = await controller.GetStocks(null, null, null, "LOC2");
            var ok3 = Assert.IsType<OkObjectResult>(res3);
            var stocks3 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Stock>>(ok3.Value);
            Assert.Single(stocks3);
            Assert.Equal("LOC2", stocks3.First().Localizacao);
        }
    }
}
