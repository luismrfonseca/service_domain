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
    public class LogisticaController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public LogisticaController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        // =========================================================================
        // 1. FLUXO DE SAÍDA (PICKING)
        // =========================================================================

        [HttpGet("picking/pendentes")]
        public async Task<IActionResult> GetPickingPendentes()
        {
            // List client sales orders that are synchronized with ERP and ready for picking
            var pendentes = await _context.Encomendas
                .Include(e => e.Linhas)
                .Where(e => e.Tipo == DocumentoTipo.EncomendaCliente && e.Status == "Sincronizado")
                .OrderBy(e => e.CreatedAt)
                .ToListAsync();

            return Ok(pendentes);
        }

        [HttpPost("picking")]
        public async Task<IActionResult> ConfirmPicking([FromBody] PickingDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                transaction = await _context.Database.BeginTransactionAsync();

                var originalOrder = await _context.Encomendas
                    .Include(e => e.Linhas)
                    .FirstOrDefaultAsync(e => e.Id == dto.EncomendaId && e.Tipo == DocumentoTipo.EncomendaCliente);

                if (originalOrder == null)
                {
                    return NotFound(new { Message = $"Encomenda de Cliente {dto.EncomendaId} não encontrada." });
                }

                if (originalOrder.Status == "Preparada")
                {
                    return BadRequest(new { Message = "Esta encomenda já foi preparada anteriormente." });
                }

                // Create the Guia de Remessa (Delivery Note / picked confirmation document)
                var guia = new Encomenda
                {
                    Tipo = DocumentoTipo.GuiaRemessa,
                    ParentId = originalOrder.Id,
                    ClienteNo = originalOrder.ClienteNo,
                    Data = DateTime.UtcNow,
                    Status = "PendenteSync",
                    DocumentoNo = 0, // Generated in ERP
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                decimal total = 0;

                foreach (var lineDto in dto.Linhas)
                {
                    var originalLine = originalOrder.Linhas.FirstOrDefault(l => l.Id == lineDto.LinhaId);
                    if (originalLine == null)
                    {
                        return BadRequest(new { Message = $"Linha de artigo {lineDto.LinhaId} não pertence à encomenda de origem." });
                    }

                    if (lineDto.QuantidadeRecolhida > originalLine.Quantidade)
                    {
                        return BadRequest(new { Message = $"Quantidade recolhida ({lineDto.QuantidadeRecolhida}) excede a quantidade pedida ({originalLine.Quantidade}) na Ref {originalLine.Ref}." });
                    }

                    // Build line for the Guia
                    var guiaLinha = new EncomendaLinha
                    {
                        Ref = originalLine.Ref,
                        Designacao = originalLine.Designacao,
                        Quantidade = lineDto.QuantidadeRecolhida,
                        Preco = originalLine.Preco,
                        Lote = lineDto.Lote,
                        Localizacao = lineDto.Localizacao,
                        ParentLineId = originalLine.Id
                    };

                    guia.Linhas.Add(guiaLinha);
                    total += lineDto.QuantidadeRecolhida * originalLine.Preco;
                }

                guia.Total = total;
                _context.Encomendas.Add(guia);

                // Update original order status to "Preparada"
                originalOrder.Status = "Preparada";
                originalOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Build outbox payload for PHC sync (will create Guia de Remessa linked to original bo)
                var syncPayload = new
                {
                    LocalId = guia.Id,
                    ParentPhcStamp = originalOrder.PhcStamp, // Crucial link to parent order in ERP
                    ClienteNo = guia.ClienteNo,
                    Total = guia.Total,
                    Data = guia.Data,
                    Linhas = guia.Linhas.Select(l => new
                    {
                        LocalLineId = l.Id,
                        ParentLinePhcStamp = originalOrder.Linhas.First(x => x.Id == l.ParentLineId).PhcStamp, // Link to order line in ERP
                        Ref = l.Ref,
                        Designacao = l.Designacao,
                        Quantidade = l.Quantidade,
                        Preco = l.Preco,
                        Lote = l.Lote,
                        Localizacao = l.Localizacao
                    }).ToList()
                };

                var outboxItem = new SyncOutbox
                {
                    EntityType = "GuiaRemessa",
                    EntityId = guia.Id,
                    Payload = JsonSerializer.Serialize(syncPayload),
                    Status = "Pendente",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SyncOutbox.Add(outboxItem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { Message = "Picking confirmado com sucesso e enfileirado para sincronização com o ERP.", GuiaId = guia.Id });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao registar o picking.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        // =========================================================================
        // 2. FLUXO DE ENTRADA (RECEÇÃO DE COMPRAS)
        // =========================================================================

        [HttpGet("rececao/pendentes")]
        public async Task<IActionResult> GetRececaoPendentes()
        {
            // List purchase orders (Encomendas de Fornecedores) that are synchronized and ready to be received
            var pendentes = await _context.Encomendas
                .Include(e => e.Linhas)
                .Where(e => e.Tipo == DocumentoTipo.EncomendaFornecedor && e.Status == "Sincronizado")
                .OrderBy(e => e.CreatedAt)
                .ToListAsync();

            return Ok(pendentes);
        }

        [HttpPost("rececao")]
        public async Task<IActionResult> ConfirmRececao([FromBody] RececaoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                transaction = await _context.Database.BeginTransactionAsync();

                var originalOrder = await _context.Encomendas
                    .Include(e => e.Linhas)
                    .FirstOrDefaultAsync(e => e.Id == dto.EncomendaId && e.Tipo == DocumentoTipo.EncomendaFornecedor);

                if (originalOrder == null)
                {
                    return NotFound(new { Message = $"Encomenda de Fornecedor {dto.EncomendaId} não encontrada." });
                }

                if (originalOrder.Status == "Recebido")
                {
                    return BadRequest(new { Message = "Esta encomenda de compra já foi recebida/faturada." });
                }

                // Create the Guia de Receção (Supplier receipt confirmation)
                var guia = new Encomenda
                {
                    Tipo = DocumentoTipo.GuiaRececao,
                    ParentId = originalOrder.Id,
                    ClienteNo = originalOrder.ClienteNo, // Vendor No
                    Data = DateTime.UtcNow,
                    Status = "PendenteSync",
                    DocumentoNo = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                decimal total = 0;

                foreach (var lineDto in dto.Linhas)
                {
                    var originalLine = originalOrder.Linhas.FirstOrDefault(l => l.Id == lineDto.LinhaId);
                    if (originalLine == null)
                    {
                        return BadRequest(new { Message = $"Linha de artigo {lineDto.LinhaId} não pertence à encomenda de fornecedor de origem." });
                    }

                    // Create line for the Guia de Receção
                    var guiaLinha = new EncomendaLinha
                    {
                        Ref = originalLine.Ref,
                        Designacao = originalLine.Designacao,
                        Quantidade = lineDto.QuantidadeRecebida,
                        Preco = originalLine.Preco,
                        Lote = lineDto.Lote,
                        Localizacao = lineDto.Localizacao,
                        ParentLineId = originalLine.Id
                    };

                    guia.Linhas.Add(guiaLinha);
                    total += lineDto.QuantidadeRecebida * originalLine.Preco;

                    // =================================================================
                    // INCREMENT LOCAL STOCK
                    // =================================================================
                    var localStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => 
                            s.Ref == originalLine.Ref && 
                            s.LoteCodigo == lineDto.Lote && 
                            s.Armazem == 1 && // Default warehouse 1
                            s.Localizacao == lineDto.Localizacao);

                    if (localStock == null)
                    {
                        localStock = new Stock
                        {
                            Ref = originalLine.Ref,
                            LoteCodigo = lineDto.Lote,
                            Armazem = 1,
                            Localizacao = lineDto.Localizacao,
                            Quantidade = lineDto.QuantidadeRecebida,
                            PhcStamp = "RECEÇÃO-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Stocks.Add(localStock);
                    }
                    else
                    {
                        localStock.Quantidade += lineDto.QuantidadeRecebida;
                        localStock.UpdatedAt = DateTime.UtcNow;
                    }
                }

                guia.Total = total;
                _context.Encomendas.Add(guia);

                // Update original purchase order status
                originalOrder.Status = "Recebido";
                originalOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Build outbox payload for PHC sync (creates Guia de Receção linked to purchase order)
                var syncPayload = new
                {
                    LocalId = guia.Id,
                    ParentPhcStamp = originalOrder.PhcStamp,
                    ClienteNo = guia.ClienteNo, // Vendor No
                    Total = guia.Total,
                    Data = guia.Data,
                    Linhas = guia.Linhas.Select(l => new
                    {
                        LocalLineId = l.Id,
                        ParentLinePhcStamp = originalOrder.Linhas.First(x => x.Id == l.ParentLineId).PhcStamp,
                        Ref = l.Ref,
                        Designacao = l.Designacao,
                        Quantidade = l.Quantidade,
                        Preco = l.Preco,
                        Lote = l.Lote,
                        Localizacao = l.Localizacao
                    }).ToList()
                };

                var outboxItem = new SyncOutbox
                {
                    EntityType = "GuiaRececao",
                    EntityId = guia.Id,
                    Payload = JsonSerializer.Serialize(syncPayload),
                    Status = "Pendente",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SyncOutbox.Add(outboxItem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { Message = "Receção de mercadoria registada localmente, stock atualizado e enfileirado para sincronização com o ERP.", GuiaId = guia.Id });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return StatusCode(500, new { Message = "Erro ao registar a receção.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
