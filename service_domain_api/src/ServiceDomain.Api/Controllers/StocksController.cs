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
    public class StocksController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public StocksController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStocks(
            [FromQuery] string? refCode,
            [FromQuery] string? loteCodigo,
            [FromQuery] int? armazem,
            [FromQuery] string? localizacao)
        {
            var query = _context.Stocks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(refCode))
            {
                query = query.Where(s => s.Ref == refCode);
            }

            if (!string.IsNullOrWhiteSpace(loteCodigo))
            {
                query = query.Where(s => s.LoteCodigo == loteCodigo);
            }

            if (armazem.HasValue)
            {
                query = query.Where(s => s.Armazem == armazem.Value);
            }

            if (!string.IsNullOrWhiteSpace(localizacao))
            {
                query = query.Where(s => s.Localizacao == localizacao);
            }

            var stocks = await query.ToListAsync();
            return Ok(stocks);
        }

        [HttpPost("movimentos")]
        public async Task<IActionResult> RecordMovement([FromBody] StockMovementDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                // Execute locally inside a transaction to keep consistency
                transaction = await _context.Database.BeginTransactionAsync();

                // Find existing local stock item or create new
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => 
                        s.Ref == dto.Ref && 
                        s.LoteCodigo == dto.LoteCodigo && 
                        s.Armazem == dto.Armazem && 
                        s.Localizacao == dto.Localizacao);

                if (stock == null)
                {
                    if (dto.Quantidade < 0)
                    {
                        return BadRequest(new { Message = "Não é possível deduzir stock de um registo inexistente localmente." });
                    }

                    stock = new Stock
                    {
                        Ref = dto.Ref,
                        LoteCodigo = dto.LoteCodigo,
                        Armazem = dto.Armazem,
                        Localizacao = dto.Localizacao,
                        Quantidade = dto.Quantidade,
                        PhcStamp = "PENDENTE-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Stocks.Add(stock);
                }
                else
                {
                    stock.Quantidade += dto.Quantidade;
                    
                    if (stock.Quantidade < 0)
                    {
                        return BadRequest(new { Message = $"Quantidade resultante negativa ({stock.Quantidade}). Movimento recusado." });
                    }

                    stock.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Generate outbox payload for synchronization to ERP PHC
                var syncPayload = new
                {
                    StockId = stock.Id,
                    Ref = dto.Ref,
                    LoteCodigo = dto.LoteCodigo,
                    Armazem = dto.Armazem,
                    Localizacao = dto.Localizacao,
                    QuantidadeMovimento = dto.Quantidade
                };

                // As stock sync is bidirectional, we'll mark this for Outbox processing if configured.
                // In a production app, the worker will insert movements into 'bo'/'bi' (as stock adjustment documents)
                // or directly update PHC stock fields depending on customization.
                var outboxItem = new SyncOutbox
                {
                    EntityType = "StockMovimento",
                    EntityId = stock.Id,
                    Payload = JsonSerializer.Serialize(syncPayload),
                    Status = "Pendente",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SyncOutbox.Add(outboxItem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { Message = "Movimento registado e enfileirado para sincronização.", ResultQuantity = stock.Quantidade });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao registar o movimento de stock.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
