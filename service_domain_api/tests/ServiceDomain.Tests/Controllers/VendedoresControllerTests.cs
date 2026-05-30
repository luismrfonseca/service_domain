using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Api.Controllers;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;
using Xunit;

namespace ServiceDomain.Tests.Controllers
{
    public class VendedoresControllerTests
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
        public async Task GetDashboard_ShouldReturnCorrectStats()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            
            // Add clients
            context.Clientes.AddRange(
                new Cliente { Id = Guid.NewGuid(), Nome = "Cliente A", NomeFiscal = "123", PhcStamp = "STAMP1" },
                new Cliente { Id = Guid.NewGuid(), Nome = "Cliente B", NomeFiscal = "456", PhcStamp = "STAMP2" }
            );

            // Add orders (some client sales orders, some supplier purchase orders)
            context.Encomendas.AddRange(
                // Client Sales Order 1 (PendenteSync)
                new Encomenda 
                { 
                    Id = Guid.NewGuid(), 
                    Tipo = DocumentoTipo.EncomendaCliente, 
                    ClienteNo = 1001, 
                    Total = 150.50m, 
                    Status = "PendenteSync", 
                    DocumentoNo = 1 
                },
                // Client Sales Order 2 (Sincronizado)
                new Encomenda 
                { 
                    Id = Guid.NewGuid(), 
                    Tipo = DocumentoTipo.EncomendaCliente, 
                    ClienteNo = 1002, 
                    Total = 250.00m, 
                    Status = "Sincronizado", 
                    DocumentoNo = 2 
                },
                // Supplier Purchase Order (Should NOT count in seller sales stats)
                new Encomenda 
                { 
                    Id = Guid.NewGuid(), 
                    Tipo = DocumentoTipo.EncomendaFornecedor, 
                    ClienteNo = 5001, 
                    Total = 1000.00m, 
                    Status = "PendenteSync", 
                    DocumentoNo = 3 
                }
            );

            await context.SaveChangesAsync();

            var controller = new VendedoresController(context);

            // Act
            var result = await controller.GetDashboard();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic stats = okResult.Value!;

            Assert.Equal(2, stats.GetType().GetProperty("TotalClientes").GetValue(stats));
            Assert.Equal(2, stats.GetType().GetProperty("TotalEncomendas").GetValue(stats));
            Assert.Equal(400.50m, stats.GetType().GetProperty("ValorTotalVendas").GetValue(stats));
            Assert.Equal(1, stats.GetType().GetProperty("EncomendasPendentesSync").GetValue(stats));
            Assert.Equal(1, stats.GetType().GetProperty("EncomendasSincronizadas").GetValue(stats));
            Assert.Equal(0, stats.GetType().GetProperty("EncomendasErroSync").GetValue(stats));
        }

        [Fact]
        public async Task GetEncomendas_ShouldReturnOnlySalesOrdersWithLines()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            
            var order1 = new Encomenda 
            { 
                Id = Guid.NewGuid(), 
                Tipo = DocumentoTipo.EncomendaCliente, 
                ClienteNo = 1001, 
                Total = 100m, 
                Status = "Sincronizado", 
                DocumentoNo = 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            order1.Linhas.Add(new EncomendaLinha { Id = Guid.NewGuid(), Ref = "P001", Designacao = "Prod 1", Quantidade = 10, Preco = 10m });

            var order2 = new Encomenda 
            { 
                Id = Guid.NewGuid(), 
                Tipo = DocumentoTipo.EncomendaFornecedor, // Supplier order (should filter out)
                ClienteNo = 5001, 
                Total = 200m, 
                Status = "PendenteSync", 
                DocumentoNo = 2,
                CreatedAt = DateTime.UtcNow
            };

            context.Encomendas.AddRange(order1, order2);
            await context.SaveChangesAsync();

            var controller = new VendedoresController(context);

            // Act
            var result = await controller.GetEncomendas();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var orders = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Encomenda>>(okResult.Value);
            
            Assert.Single(orders);
            var returnedOrder = orders.First();
            Assert.Equal(1001, returnedOrder.ClienteNo);
            Assert.Equal(DocumentoTipo.EncomendaCliente, returnedOrder.Tipo);
            Assert.Single(returnedOrder.Linhas);
            Assert.Equal("P001", returnedOrder.Linhas.First().Ref);
        }
    }
}
