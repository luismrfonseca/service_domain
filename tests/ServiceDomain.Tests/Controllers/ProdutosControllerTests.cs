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
    public class ProdutosControllerTests
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
        public async Task GetProdutos_ShouldReturnAllProducts_WhenNoFiltersAreProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Produtos.AddRange(
                new Produto { Ref = "PROD1", Designacao = "Produto 1", PhcStamp = "STAMP1" },
                new Produto { Ref = "PROD2", Designacao = "Produto 2", PhcStamp = "STAMP2" }
            );
            await context.SaveChangesAsync();

            var controller = new ProdutosController(context);

            // Act
            var result = await controller.GetProdutos(null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var products = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Produto>>(okResult.Value);
            Assert.Equal(2, products.Count());
        }

        [Fact]
        public async Task GetProdutos_ShouldFilterByRef_WhenSearchRefIsProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Produtos.AddRange(
                new Produto { Ref = "PROD1", Designacao = "Produto A", PhcStamp = "STAMP1" },
                new Produto { Ref = "TEST2", Designacao = "Produto B", PhcStamp = "STAMP2" }
            );
            await context.SaveChangesAsync();

            var controller = new ProdutosController(context);

            // Act
            var result = await controller.GetProdutos("PROD", null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var products = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Produto>>(okResult.Value);
            Assert.Single(products);
            Assert.Equal("PROD1", products.First().Ref);
        }

        [Fact]
        public async Task GetProdutos_ShouldFilterByDesignacao_WhenSearchDesignacaoIsProvided()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Produtos.AddRange(
                new Produto { Ref = "PROD1", Designacao = "Sapato Azul", PhcStamp = "STAMP1" },
                new Produto { Ref = "PROD2", Designacao = "Camisa Vermelha", PhcStamp = "STAMP2" }
            );
            await context.SaveChangesAsync();

            var controller = new ProdutosController(context);

            // Act
            var result = await controller.GetProdutos(null, "Azul");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var products = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Produto>>(okResult.Value);
            Assert.Single(products);
            Assert.Equal("Sapato Azul", products.First().Designacao);
        }
    }
}
