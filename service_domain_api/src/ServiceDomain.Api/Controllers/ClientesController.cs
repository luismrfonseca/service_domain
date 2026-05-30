using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Api.Models;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;

namespace ServiceDomain.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public ClientesController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetClientes()
        {
            var clientes = await _context.Clientes.ToListAsync();
            return Ok(clientes);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCliente([FromBody] CreateClienteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                // Start local transaction to ensure ACID compile of Local Client and Outbox record
                transaction = await _context.Database.BeginTransactionAsync();

                // 1. Create client locally (temporary placeholders for PhcStamp and No)
                var cliente = new Cliente
                {
                    Nome = dto.Nome,
                    NomeFiscal = dto.NomeFiscal,
                    Email = dto.Email,
                    No = 0, // Will be updated by PHC during synchronization
                    PhcStamp = "PENDENTE-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // 2. Prepare payload for the Outbox
                var syncPayload = new
                {
                    LocalId = cliente.Id,
                    Nome = cliente.Nome,
                    NomeFiscal = cliente.NomeFiscal,
                    Email = cliente.Email
                };

                var outboxItem = new SyncOutbox
                {
                    EntityType = "Cliente",
                    EntityId = cliente.Id,
                    Payload = JsonSerializer.Serialize(syncPayload),
                    Status = "Pendente",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SyncOutbox.Add(outboxItem);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                return Accepted(new { Message = "Cliente criado localmente. Sincronização pendente com o ERP.", LocalId = cliente.Id });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao registar o cliente localmente.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCliente(Guid id, [FromBody] CreateClienteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound(new { Message = $"Cliente com ID {id} não encontrado." });
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                transaction = await _context.Database.BeginTransactionAsync();

                // Update local model
                cliente.Nome = dto.Nome;
                cliente.NomeFiscal = dto.NomeFiscal;
                cliente.Email = dto.Email;
                cliente.UpdatedAt = DateTime.UtcNow;

                _context.Clientes.Update(cliente);
                await _context.SaveChangesAsync();

                // Check sync status
                if (cliente.PhcStamp.StartsWith("PENDENTE-"))
                {
                    // Client has not synced to PHC yet. We find its pending outbox message and update it
                    var pendingOutbox = await _context.SyncOutbox
                        .FirstOrDefaultAsync(o => o.EntityType == "Cliente" && o.EntityId == cliente.Id && o.Status == "Pendente");
                    
                    if (pendingOutbox != null)
                    {
                        var syncPayload = new
                        {
                            LocalId = cliente.Id,
                            Nome = cliente.Nome,
                            NomeFiscal = cliente.NomeFiscal,
                            Email = cliente.Email
                        };
                        pendingOutbox.Payload = JsonSerializer.Serialize(syncPayload);
                        _context.SyncOutbox.Update(pendingOutbox);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        await CreateClienteUpdateOutboxAsync(cliente);
                    }
                }
                else
                {
                    await CreateClienteUpdateOutboxAsync(cliente);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Cliente atualizado com sucesso localmente.", LocalId = cliente.Id });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao atualizar o cliente localmente.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private async Task CreateClienteUpdateOutboxAsync(Cliente cliente)
        {
            var syncPayload = new
            {
                LocalId = cliente.Id,
                PhcStamp = cliente.PhcStamp,
                Nome = cliente.Nome,
                NomeFiscal = cliente.NomeFiscal,
                Email = cliente.Email
            };

            var outboxItem = new SyncOutbox
            {
                EntityType = "ClienteUpdate",
                EntityId = cliente.Id,
                Payload = JsonSerializer.Serialize(syncPayload),
                Status = "Pendente",
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.SyncOutbox.Add(outboxItem);
            await _context.SaveChangesAsync();
        }
    }
}
