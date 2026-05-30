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
    public class ClientesControllerTests
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
        public async Task CreateCliente_ShouldPersistClienteAndCreateOutboxItem_WhenDtoIsValid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new ClientesController(context);
            var dto = new CreateClienteDto
            {
                Nome = "Cliente de Teste",
                NomeFiscal = "PT123456789",
                Email = "teste@cliente.com"
            };

            // Act
            var result = await controller.CreateCliente(dto);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            dynamic value = acceptedResult.Value!;
            Guid localId = value.GetType().GetProperty("LocalId").GetValue(value);

            // Verify local client was created
            var client = await context.Clientes.FindAsync(localId);
            Assert.NotNull(client);
            Assert.Equal("Cliente de Teste", client.Nome);
            Assert.Equal("PT123456789", client.NomeFiscal);
            Assert.Equal("teste@cliente.com", client.Email);
            Assert.StartsWith("PENDENTE-", client.PhcStamp);

            // Verify SyncOutbox item was generated
            var outboxItem = await context.SyncOutbox.FirstOrDefaultAsync(x => x.EntityId == localId);
            Assert.NotNull(outboxItem);
            Assert.Equal("Cliente", outboxItem.EntityType);
            Assert.Equal("Pendente", outboxItem.Status);
            Assert.Contains("Cliente de Teste", outboxItem.Payload);
        }

        [Fact]
        public async Task CreateCliente_ShouldReturn500_WhenDatabaseErrorOccurs()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new ClientesController(context);
            var dto = new CreateClienteDto
            {
                Nome = "Cliente Falha",
                NomeFiscal = "PT999999999"
            };

            // We mock a database error by adding a client with a duplicate unique key (PhcStamp)
            // so SaveChangesAsync throws an DbUpdateException.
            context.Clientes.Add(new Cliente 
            { 
                Id = Guid.NewGuid(), 
                Nome = "Original", 
                NomeFiscal = "123", 
                PhcStamp = "DUPLICADO" 
            });
            await context.SaveChangesAsync();

            // We patch the controller to create a duplicate stamp (we can override GUID or force error by disposing the context
            // but catching the exception properly is the test goal)
            context.Dispose();

            // Act
            var result = await controller.CreateCliente(dto);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetClientes_ShouldReturnAllClientes()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            context.Clientes.AddRange(
                new Cliente { Id = Guid.NewGuid(), Nome = "Cliente A", NomeFiscal = "123", PhcStamp = "STAMP1" },
                new Cliente { Id = Guid.NewGuid(), Nome = "Cliente B", NomeFiscal = "456", PhcStamp = "STAMP2" }
            );
            await context.SaveChangesAsync();

            var controller = new ClientesController(context);

            // Act
            var result = await controller.GetClientes();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var clientes = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<Cliente>>(okResult.Value);
            Assert.Equal(2, clientes.Count());
        }

        [Fact]
        public async Task UpdateCliente_ShouldUpdatePendingOutbox_WhenClientNotSynced()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var clientId = Guid.NewGuid();
            var cliente = new Cliente 
            { 
                Id = clientId, 
                Nome = "Original Nome", 
                NomeFiscal = "PT111", 
                Email = "orig@email.com", 
                PhcStamp = "PENDENTE-12345", 
                No = 0 
            };
            var pendingOutbox = new SyncOutbox
            {
                Id = Guid.NewGuid(),
                EntityType = "Cliente",
                EntityId = clientId,
                Payload = "{}",
                Status = "Pendente",
                CreatedAt = DateTime.UtcNow
            };

            context.Clientes.Add(cliente);
            context.SyncOutbox.Add(pendingOutbox);
            await context.SaveChangesAsync();

            var controller = new ClientesController(context);
            var dto = new CreateClienteDto
            {
                Nome = "Novo Nome",
                NomeFiscal = "PT222",
                Email = "novo@email.com"
            };

            // Act
            var result = await controller.UpdateCliente(clientId, dto);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            // Verify local DB updated
            var updatedClient = await context.Clientes.FindAsync(clientId);
            Assert.NotNull(updatedClient);
            Assert.Equal("Novo Nome", updatedClient.Nome);
            Assert.Equal("PT222", updatedClient.NomeFiscal);
            Assert.Equal("novo@email.com", updatedClient.Email);

            // Verify existing outbox item payload updated
            var updatedOutbox = await context.SyncOutbox.FindAsync(pendingOutbox.Id);
            Assert.NotNull(updatedOutbox);
            Assert.Contains("Novo Nome", updatedOutbox.Payload);
            Assert.Contains("PT222", updatedOutbox.Payload);
        }

        [Fact]
        public async Task UpdateCliente_ShouldCreateClienteUpdateOutbox_WhenClientIsAlreadySynced()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var clientId = Guid.NewGuid();
            var cliente = new Cliente 
            { 
                Id = clientId, 
                Nome = "Original Nome", 
                NomeFiscal = "PT111", 
                Email = "orig@email.com", 
                PhcStamp = "ERPSTAMP999", 
                No = 123 
            };

            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();

            var controller = new ClientesController(context);
            var dto = new CreateClienteDto
            {
                Nome = "Novo Nome Synced",
                NomeFiscal = "PT333",
                Email = "novo.synced@email.com"
            };

            // Act
            var result = await controller.UpdateCliente(clientId, dto);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            // Verify new ClienteUpdate Outbox message created
            var outboxItem = await context.SyncOutbox.FirstOrDefaultAsync(o => o.EntityType == "ClienteUpdate" && o.EntityId == clientId);
            Assert.NotNull(outboxItem);
            Assert.Equal("Pendente", outboxItem.Status);
            Assert.Contains("Novo Nome Synced", outboxItem.Payload);
            Assert.Contains("ERPSTAMP999", outboxItem.Payload);
        }
    }
}
