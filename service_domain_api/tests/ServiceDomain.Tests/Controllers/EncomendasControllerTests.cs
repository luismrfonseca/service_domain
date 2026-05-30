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
    public class EncomendasControllerTests
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
        public async Task CreateEncomenda_ShouldPersistOrderAndLines_AndWriteToOutbox()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new EncomendasController(context);

            var dto = new CreateEncomendaDto
            {
                ClienteNo = 1001,
                Linhas = new List<EncomendaLinhaDto>
                {
                    new EncomendaLinhaDto { Ref = "PROD1", Designacao = "Prod 1", Quantidade = 2, Preco = 10.5m },
                    new EncomendaLinhaDto { Ref = "PROD2", Designacao = "Prod 2", Quantidade = 5, Preco = 2.0m }
                }
            };

            // Act
            var result = await controller.CreateEncomenda(dto);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedAtActionResult>(result);
            dynamic value = acceptedResult.Value!;
            Guid localId = value.GetType().GetProperty("LocalId").GetValue(value);
            decimal total = value.GetType().GetProperty("Total").GetValue(value);

            // Verify total computed: (2 * 10.5) + (5 * 2.0) = 21.0 + 10.0 = 31.0
            Assert.Equal(31.0m, total);

            // Verify order header in database
            var order = await context.Encomendas.Include(e => e.Linhas).FirstOrDefaultAsync(e => e.Id == localId);
            Assert.NotNull(order);
            Assert.Equal(1001, order.ClienteNo);
            Assert.Equal(31.0m, order.Total);
            Assert.Equal("PendenteSync", order.Status);
            Assert.Equal(DocumentoTipo.EncomendaCliente, order.Tipo);
            Assert.Equal(2, order.Linhas.Count);

            // Verify lines
            var line1 = order.Linhas.FirstOrDefault(l => l.Ref == "PROD1");
            Assert.NotNull(line1);
            Assert.Equal(2, line1.Quantidade);
            Assert.Equal(10.5m, line1.Preco);

            // Verify SyncOutbox message
            var outboxItem = await context.SyncOutbox.FirstOrDefaultAsync(x => x.EntityId == localId);
            Assert.NotNull(outboxItem);
            Assert.Equal("Encomenda", outboxItem.EntityType);
            Assert.Equal("Pendente", outboxItem.Status);
            Assert.Contains("PROD1", outboxItem.Payload);
        }

        [Fact]
        public async Task GetEncomendaById_ShouldReturnOrder_WhenIdExists()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var existingId = Guid.NewGuid();
            var order = new Encomenda
            {
                Id = existingId,
                ClienteNo = 2002,
                DocumentoNo = 1234,
                Total = 100m,
                Status = "Sincronizado"
            };
            context.Encomendas.Add(order);
            await context.SaveChangesAsync();

            var controller = new EncomendasController(context);

            // Act
            var result = await controller.GetEncomendaById(existingId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedOrder = Assert.IsType<Encomenda>(okResult.Value);
            Assert.Equal(existingId, returnedOrder.Id);
            Assert.Equal(2002, returnedOrder.ClienteNo);
        }

        [Fact]
        public async Task GetEncomendaById_ShouldReturnNotFound_WhenIdDoesNotExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new EncomendasController(context);

            // Act
            var result = await controller.GetEncomendaById(Guid.NewGuid());

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetEncomendas_ShouldReturnFilteredEncomendas_WhenFiltersAreProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Encomendas.AddRange(
                new Encomenda { Id = Guid.NewGuid(), ClienteNo = 1001, DocumentoNo = 1, Status = "Sincronizado" },
                new Encomenda { Id = Guid.NewGuid(), ClienteNo = 1002, DocumentoNo = 2, Status = "PendenteSync" }
            );
            await context.SaveChangesAsync();

            var controller = new EncomendasController(context);

            // Act & Assert 1: No filters
            var res1 = await controller.GetEncomendas(null, null);
            var ok1 = Assert.IsType<OkObjectResult>(res1);
            var encs1 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(ok1.Value);
            Assert.Equal(2, encs1.Count());

            // Act & Assert 2: Filter by clienteNo
            var res2 = await controller.GetEncomendas(1001, null);
            var ok2 = Assert.IsType<OkObjectResult>(res2);
            var encs2 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(ok2.Value);
            Assert.Single(encs2);
            Assert.Equal(1001, encs2.First().ClienteNo);

            // Act & Assert 3: Filter by status
            var res3 = await controller.GetEncomendas(null, "PendenteSync");
            var ok3 = Assert.IsType<OkObjectResult>(res3);
            var encs3 = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(ok3.Value);
            Assert.Single(encs3);
            Assert.Equal("PendenteSync", encs3.First().Status);
        }
    }
}
