using System;
using System.Linq;
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
    public class EncomendasController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public EncomendasController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetEncomendas([FromQuery] int? clienteNo, [FromQuery] string? status)
        {
            var query = _context.Encomendas.AsQueryable();

            if (clienteNo.HasValue)
            {
                query = query.Where(e => e.ClienteNo == clienteNo.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.Status == status);
            }

            var encomendas = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
            return Ok(encomendas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEncomendaById(Guid id)
        {
            var encomenda = await _context.Encomendas
                .Include(e => e.Linhas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (encomenda == null)
            {
                return NotFound(new { Message = $"Encomenda com ID {id} não encontrada." });
            }

            return Ok(encomenda);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEncomenda([FromBody] CreateEncomendaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                // Start a local database transaction to ensure atomicity
                transaction = await _context.Database.BeginTransactionAsync();

                // Calculate total of the order based on lines
                decimal total = dto.Linhas.Sum(l => l.Quantidade * l.Preco);

                // Create the Encomenda entity
                var encomenda = new Encomenda
                {
                    ClienteNo = dto.ClienteNo,
                    Data = DateTime.UtcNow,
                    Total = total,
                    Status = "PendenteSync",
                    PhcStamp = null, // Will be filled once synced to PHC
                    DocumentoNo = 0, // Temporarily 0; worker updates this from PHC boconf counter during sync
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Add lines
                foreach (var lineDto in dto.Linhas)
                {
                    encomenda.Linhas.Add(new EncomendaLinha
                    {
                        Ref = lineDto.Ref,
                        Designacao = lineDto.Designacao,
                        Quantidade = lineDto.Quantidade,
                        Preco = lineDto.Preco,
                        Lote = lineDto.Lote,
                        PhcStamp = null // Will be filled once synced
                    });
                }

                _context.Encomendas.Add(encomenda);
                await _context.SaveChangesAsync();

                // Build outbox payload representing the order to sync
                var syncPayload = new
                {
                    LocalId = encomenda.Id,
                    ClienteNo = encomenda.ClienteNo,
                    Total = encomenda.Total,
                    Data = encomenda.Data,
                    Linhas = encomenda.Linhas.Select(l => new
                    {
                        LocalLineId = l.Id,
                        Ref = l.Ref,
                        Designacao = l.Designacao,
                        Quantidade = l.Quantidade,
                        Preco = l.Preco,
                        Lote = l.Lote
                    }).ToList()
                };

                var outboxItem = new SyncOutbox
                {
                    EntityType = "Encomenda",
                    EntityId = encomenda.Id,
                    Payload = JsonSerializer.Serialize(syncPayload),
                    Status = "Pendente",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SyncOutbox.Add(outboxItem);
                await _context.SaveChangesAsync();

                // Commit the local transaction
                await transaction.CommitAsync();

                return AcceptedAtAction(
                    nameof(GetEncomendaById), 
                    new { id = encomenda.Id }, 
                    new { Message = "Encomenda registada com sucesso localmente. Sincronização em curso.", LocalId = encomenda.Id, Total = total }
                );
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao submeter a encomenda localmente.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
