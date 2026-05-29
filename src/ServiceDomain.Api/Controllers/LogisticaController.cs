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

        private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();

        // =========================================================================
        // 3. GS1 DECODING & DIRECTED PUTAWAY
        // =========================================================================

        [HttpPost("gs1/decode")]
        public IActionResult DecodeGs1([FromBody] Gs1DecodeDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = ParseGs1Barcode(dto.RawBarcode);
            return Ok(result);
        }

        [HttpGet("putaway/sugestao")]
        public async Task<IActionResult> GetPutawaySugestao([FromQuery] string refCode, [FromQuery] decimal quantidade)
        {
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Ref == refCode);
            if (produto == null)
            {
                return NotFound(new { Message = $"Produto {refCode} não encontrado." });
            }

            // 1. If product requires CQ, suggest CQ location
            if (produto.RequerCq)
            {
                var cqLoc = await _context.Localizacoes
                    .Where(l => l.Zona == "CQ")
                    .FirstOrDefaultAsync();

                return Ok(new PutawaySuggestionDto
                {
                    LocalizacaoId = cqLoc?.Nome ?? "CQ-GERAL",
                    Zona = "CQ",
                    Corredor = cqLoc?.Corredor ?? "0",
                    Estante = cqLoc?.Estante ?? "0",
                    Prateleira = cqLoc?.Prateleira ?? "0",
                    Alveolo = cqLoc?.Alveolo ?? "0",
                    MaxPesoKg = cqLoc?.MaxPesoKg ?? 1000m,
                    MaxVolumeM3 = cqLoc?.MaxVolumeM3 ?? 10m,
                    Reason = "Artigo Requer CQ (Controlo de Qualidade)."
                });
            }

            // 2. Find optimal location matching product class zone with space capacity
            var locs = await _context.Localizacoes
                .Where(l => l.Zona == produto.ClasseAbc.ToString() || l.Zona == "GERAL")
                .ToListAsync();

            foreach (var loc in locs)
            {
                var stocks = await _context.Stocks
                    .Where(s => s.Localizacao == loc.Nome)
                    .ToListAsync();

                decimal currentWeight = 0;
                decimal currentVolume = 0;

                foreach (var st in stocks)
                {
                    var p = await _context.Produtos.FirstOrDefaultAsync(x => x.Ref == st.Ref);
                    if (p != null)
                    {
                        currentWeight += st.Quantidade * p.PesoUnitarioKg;
                        currentVolume += st.Quantidade * p.VolumeUnitarioM3;
                    }
                }

                decimal addedWeight = quantidade * produto.PesoUnitarioKg;
                decimal addedVolume = quantidade * produto.VolumeUnitarioM3;

                if (currentWeight + addedWeight <= loc.MaxPesoKg && currentVolume + addedVolume <= loc.MaxVolumeM3)
                {
                    return Ok(new PutawaySuggestionDto
                    {
                        LocalizacaoId = loc.Nome,
                        Zona = loc.Zona,
                        Corredor = loc.Corredor,
                        Estante = loc.Estante,
                        Prateleira = loc.Prateleira,
                        Alveolo = loc.Alveolo,
                        MaxPesoKg = loc.MaxPesoKg,
                        MaxVolumeM3 = loc.MaxVolumeM3,
                        Reason = $"Espaço disponível na Zona {loc.Zona} compatível com Classe {produto.ClasseAbc}."
                    });
                }
            }

            // Fallback to first available location in class
            var defaultLoc = locs.FirstOrDefault();
            if (defaultLoc != null)
            {
                return Ok(new PutawaySuggestionDto
                {
                    LocalizacaoId = defaultLoc.Nome,
                    Zona = defaultLoc.Zona,
                    Corredor = defaultLoc.Corredor,
                    Estante = defaultLoc.Estante,
                    Prateleira = defaultLoc.Prateleira,
                    Alveolo = defaultLoc.Alveolo,
                    MaxPesoKg = defaultLoc.MaxPesoKg,
                    MaxVolumeM3 = defaultLoc.MaxVolumeM3,
                    Reason = "Localização padrão da classe (limites ignorados devido a capacidade esgotada)."
                });
            }

            return Ok(new PutawaySuggestionDto
            {
                LocalizacaoId = "GERAL",
                Zona = "GERAL",
                Reason = "Localização GERAL recomendada (sem correspondência específica)."
            });
        }

        private Gs1ResultDto ParseGs1Barcode(string rawBarcode)
        {
            var result = new Gs1ResultDto
            {
                Raw = rawBarcode
            };

            int i = 0;
            while (i < rawBarcode.Length)
            {
                if (i + 2 > rawBarcode.Length) break;

                string ai = rawBarcode.Substring(i, 2);
                if (ai == "01")
                {
                    if (i + 16 <= rawBarcode.Length)
                    {
                        result.Gtin = rawBarcode.Substring(i + 2, 14);
                        i += 16;
                    }
                    else i++;
                }
                else if (ai == "17")
                {
                    if (i + 8 <= rawBarcode.Length)
                    {
                        string dateStr = rawBarcode.Substring(i + 2, 6);
                        string year = "20" + dateStr.Substring(0, 2);
                        string month = dateStr.Substring(2, 2);
                        string day = dateStr.Substring(4, 2);

                        if (day == "00")
                        {
                            result.ValidationError = "Erro Regulatório GS1: Data de validade não pode conter o dia 00. Introduza o dia manualmente.";
                        }

                        result.ExpiryDate = $"{year}-{month}-{day}";
                        i += 8;
                    }
                    else i++;
                }
                else if (ai == "10")
                {
                    string lotData = "";
                    int j = i + 2;
                    while (j < rawBarcode.Length && rawBarcode[j] != '\x1D')
                    {
                        lotData += rawBarcode[j];
                        j++;
                    }
                    result.Lot = lotData;
                    i = (j < rawBarcode.Length && rawBarcode[j] == '\x1D') ? j + 1 : j;
                }
                else if (ai == "37")
                {
                    string qtyData = "";
                    int j = i + 2;
                    while (j < rawBarcode.Length && rawBarcode[j] != '\x1D')
                    {
                        qtyData += rawBarcode[j];
                        j++;
                    }
                    if (int.TryParse(qtyData, out int qty))
                    {
                        result.Quantity = qty;
                    }
                    i = (j < rawBarcode.Length && rawBarcode[j] == '\x1D') ? j + 1 : j;
                }
                else
                {
                    i++;
                }
            }

            return result;
        }

        // =========================================================================
        // 4. INVENTÁRIO & CONTAGEM CÍCLICA
        // =========================================================================

        [HttpGet("contagens/pendentes")]
        public async Task<IActionResult> GetContagensPendentes()
        {
            var ordens = await _context.OrdensContagem
                .Include(o => o.Linhas)
                .ThenInclude(l => l.Stock)
                .Where(o => o.Estado != "CONCLUIDO")
                .ToListAsync();

            return Ok(ordens);
        }

        [HttpPost("contagens")]
        public async Task<IActionResult> CriarContagem([FromBody] ContagemCriarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ordem = new OrdemContagem
            {
                TipoContagem = dto.TipoContagem,
                SupervisorId = dto.SupervisorId,
                Estado = "PENDENTE",
                DataCriacao = DateTime.UtcNow
            };

            foreach (var stockId in dto.StockIds)
            {
                var stock = await _context.Stocks.FindAsync(stockId);
                if (stock == null)
                {
                    return BadRequest(new { Message = $"Registo de stock {stockId} não encontrado." });
                }

                ordem.Linhas.Add(new LinhaContagem
                {
                    StockId = stockId,
                    QuantidadeSistema = stock.Quantidade,
                    AjusteAplicado = false
                });
            }

            _context.OrdensContagem.Add(ordem);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Ordem de contagem criada com sucesso.", OrdemId = ordem.Id });
        }

        [HttpPost("contagens/registar")]
        public async Task<IActionResult> RegistarContagem([FromBody] ContagemRegistarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ordem = await _context.OrdensContagem
                .Include(o => o.Linhas)
                .FirstOrDefaultAsync(o => o.Id == dto.OrdemId);

            if (ordem == null)
            {
                return NotFound(new { Message = $"Ordem de contagem {dto.OrdemId} não encontrada." });
            }

            var linha = ordem.Linhas.FirstOrDefault(l => l.Id == dto.LinhaId);
            if (linha == null)
            {
                return NotFound(new { Message = $"Linha de contagem {dto.LinhaId} não pertence a esta ordem." });
            }

            if (linha.QuantidadeContada1 == null)
            {
                // Primeira contagem
                linha.QuantidadeContada1 = dto.QuantidadeContada;
                linha.Operador1Id = dto.OperadorId;

                // Verificar discrepância
                if (dto.QuantidadeContada != linha.QuantidadeSistema)
                {
                    ordem.Estado = "EM_RECONTAGEM";
                }
            }
            else if (linha.QuantidadeContada2 == null)
            {
                // Recontagem cega
                if (dto.OperadorId == linha.Operador1Id)
                {
                    return BadRequest(new { Message = "A recontagem cega deve ser realizada por um operador independente." });
                }

                linha.QuantidadeContada2 = dto.QuantidadeContada;
                linha.Operador2Id = dto.OperadorId;
            }
            else
            {
                return BadRequest(new { Message = "Esta linha já foi totalmente contada e aguarda aprovação." });
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Contagem registada com sucesso.", EstadoOrdem = ordem.Estado });
        }

        [HttpPost("contagens/aprovar")]
        public async Task<IActionResult> AprovarContagem([FromBody] ContagemAprovarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ordem = await _context.OrdensContagem
                .Include(o => o.Linhas)
                .ThenInclude(l => l.Stock)
                .FirstOrDefaultAsync(o => o.Id == dto.OrdemId);

            if (ordem == null)
            {
                return NotFound(new { Message = $"Ordem de contagem {dto.OrdemId} não encontrada." });
            }

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                transaction = await _context.Database.BeginTransactionAsync();

                ordem.SupervisorId = dto.SupervisorId;
                ordem.Estado = "CONCLUIDO";

                foreach (var linha in ordem.Linhas)
                {
                    linha.DataAprovacao = DateTime.UtcNow;
                    linha.AjusteAplicado = true;

                    // Escolher quantidade final (contada 2 se existir recontagem cega, senão contada 1)
                    decimal finalQty = linha.QuantidadeContada2 ?? linha.QuantidadeContada1 ?? linha.QuantidadeSistema;

                    // Reconciliar Stock
                    linha.Stock.Quantidade = finalQty;
                    linha.Stock.DataUltimaContagem = DateTime.UtcNow;
                    linha.Stock.UpdatedAt = DateTime.UtcNow;

                    // Gerar payload outbox para sincronização com ERP
                    var syncPayload = new
                    {
                        StockId = linha.StockId,
                        Ref = linha.Stock.Ref,
                        LoteCodigo = linha.Stock.LoteCodigo,
                        Armazem = linha.Stock.Armazem,
                        Localizacao = linha.Stock.Localizacao,
                        QuantidadeFinal = finalQty,
                        Diferenca = finalQty - linha.QuantidadeSistema,
                        SupervisorId = dto.SupervisorId
                    };

                    _context.SyncOutbox.Add(new SyncOutbox
                    {
                        EntityType = "StockAjuste",
                        EntityId = linha.Stock.Id,
                        Payload = JsonSerializer.Serialize(syncPayload),
                        Status = "Pendente",
                        RetryCount = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Ordem de contagem aprovada e stock reconciliado localmente com sucesso." });
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Erro ao aprovar contagem.", Error = ex.Message });
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        // =========================================================================
        // 5. PICKING OTIMIZADO (S-SHAPE ROUTING)
        // =========================================================================

        [HttpGet("picking/optimized/{id}")]
        public async Task<IActionResult> GetPickingOptimized(Guid id)
        {
            var order = await _context.Encomendas
                .Include(e => e.Linhas)
                .FirstOrDefaultAsync(e => e.Id == id && e.Tipo == DocumentoTipo.EncomendaCliente);

            if (order == null)
            {
                return NotFound(new { Message = $"Encomenda {id} não encontrada." });
            }

            var lines = order.Linhas.ToList();
            var locNames = lines.Select(l => l.Localizacao).Distinct().ToList();
            
            var dbLocs = await _context.Localizacoes
                .Where(l => locNames.Contains(l.Nome))
                .ToListAsync();

            var lineLocPairs = lines.Select(line =>
            {
                var loc = dbLocs.FirstOrDefault(l => l.Nome == line.Localizacao) ?? new Localizacao
                {
                    Nome = line.Localizacao ?? "GERAL",
                    Corredor = "1",
                    Estante = "1"
                };
                return new { Line = line, Loc = loc };
            }).ToList();

            // S-Shape (Serpentina) sorting
            var sortedPairs = lineLocPairs.OrderBy(x =>
            {
                int.TryParse(x.Loc.Corredor, out int corr);
                return corr;
            }).ThenBy(x =>
            {
                int.TryParse(x.Loc.Corredor, out int corr);
                int.TryParse(x.Loc.Estante, out int est);
                return corr % 2 == 1 ? est : -est;
            }).ToList();

            var result = sortedPairs.Select(x => x.Line).ToList();
            return Ok(result);
        }

        // =========================================================================
        // 6. PACKING STATION & WEIGHT VALIDATION
        // =========================================================================

        [HttpPost("packing/validate")]
        public IActionResult ValidatePackingWeight([FromBody] ValidatePackingWeightDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            decimal theoreticalWeight = dto.BoxTare;

            foreach (var item in dto.PackedItems)
            {
                theoreticalWeight += item.Quantidade * item.PesoUnitarioKg;
            }

            decimal maxDeviation = theoreticalWeight * (dto.TolerancePercent / 100);
            decimal minAllowed = theoreticalWeight - maxDeviation;
            decimal maxAllowed = theoreticalWeight + maxDeviation;

            bool isValid = dto.ActualWeight >= minAllowed && dto.ActualWeight <= maxAllowed;
            decimal deviation = dto.ActualWeight - theoreticalWeight;

            return Ok(new
            {
                IsValid = isValid,
                TheoreticalWeight = theoreticalWeight,
                MinAllowed = minAllowed,
                MaxAllowed = maxAllowed,
                Deviation = deviation
            });
        }

        // =========================================================================
        // 7. TAX AUTHORITY (AT SOAP) & CARRIERS (ZPL)
        // =========================================================================

        [HttpPost("shipping/at-comunicar")]
        public IActionResult ComunicarGuiaTransporteAT([FromBody] JsonElement payload)
        {
            // Simulate NTP Clock check
            bool isClockSynced = true; // Simulated NTP check (hora.oal.ul.pt)
            if (!isClockSynced)
            {
                return BadRequest(new { Message = "Falha na sincronização NTP com hora.oal.ul.pt. Rejeitado pela AT." });
            }

            // Generate mock AT SOAP transaction ID (12 chars alphanumeric)
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string atCode = new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());

            // Build mock SOAP response envelope for visualization
            string mockSoapResponse = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:doc=""https://servicos.portaldasfinancas.gov.pt/sgdtws/documentosTransporte"">
   <soapenv:Body>
      <doc:envioDocumentoTransporteResponse>
         <doc:CodigoIdentificacaoAT>{atCode}</doc:CodigoIdentificacaoAT>
         <doc:EstadoDocumento>Comunicado com Sucesso</doc:EstadoDocumento>
      </doc:envioDocumentoTransporteResponse>
   </soapenv:Body>
</soapenv:Envelope>";

            return Ok(new
            {
                Message = "Documento de transporte comunicado com sucesso à Autoridade Tributária.",
                CodigoAT = atCode,
                Estado = "Comunicado com Sucesso",
                SoapEnvelope = mockSoapResponse
            });
        }

        [HttpPost("shipping/carrier-label")]
        public IActionResult GerarEtiquetaTransportadora([FromBody] JsonElement payload)
        {
            string carrierService = payload.GetProperty("carrier_service").GetString() ?? "CTT_EXPRESSO_13H";
            string trackNum = "TRK-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            // Mock ZPL content
            string zpl = $@"^XA
^CF0,30
^FO50,50^FD{carrierService}^FS
^FO50,100^FDTRACKING: {trackNum}^FS
^FO50,150^FDDESTINATARIO: {payload.GetProperty("recipient").GetProperty("name").GetString()}^FS
^BY3,2,100^FO50,220^BCN,100,Y,N,N^FD{trackNum}^FS
^XZ";

            return Ok(new
            {
                TrackingNumber = trackNum,
                ZplLabel = zpl,
                Carrier = carrierService
            });
        }

        // =========================================================================
        // 8. LOGÍSTICA INVERSA & RMA DEVOLUÇÕES
        // =========================================================================

        [HttpGet("rma")]
        public async Task<IActionResult> GetRmas()
        {
            var rmas = await _context.Rmas.Include(r => r.Linhas).ToListAsync();
            return Ok(rmas);
        }

        [HttpPost("rma")]
        public async Task<IActionResult> CriarRma([FromBody] RmaCriarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rma = new Rma
            {
                RmaCodigo = dto.RmaCodigo,
                InvoiceRef = dto.InvoiceRef,
                ClienteNo = dto.ClienteNo,
                Status = "Iniciada",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var line in dto.Linhas)
            {
                rma.Linhas.Add(new RmaLinha
                {
                    Ref = line.Ref,
                    Quantidade = line.Quantidade
                });
            }

            _context.Rmas.Add(rma);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "RMA registada como Rascunho (Iniciada).", RmaId = rma.Id, Codigo = rma.RmaCodigo });
        }

        [HttpPost("rma/status")]
        public async Task<IActionResult> UpdateRmaStatus([FromBody] JsonElement payload)
        {
            Guid rmaId = payload.GetProperty("RmaId").GetGuid();
            string newStatus = payload.GetProperty("Status").GetString() ?? "Autorizada";

            var rma = await _context.Rmas.FindAsync(rmaId);
            if (rma == null) return NotFound(new { Message = "RMA não encontrada." });

            rma.Status = newStatus;
            rma.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Estado do RMA atualizado para {newStatus}.", Status = rma.Status });
        }

        [HttpPost("rma/grade")]
        public async Task<IActionResult> GradeRmaLine([FromBody] RmaGradeDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rma = await _context.Rmas
                .Include(r => r.Linhas)
                .FirstOrDefaultAsync(r => r.Id == dto.RmaId);

            if (rma == null) return NotFound(new { Message = "RMA não encontrada." });

            var linha = rma.Linhas.FirstOrDefault(l => l.Id == dto.LinhaId);
            if (linha == null) return NotFound(new { Message = "Linha de RMA não encontrada." });

            linha.Grading = dto.Grading;

            // Route physical stock location based on Grading Matrix
            string destinationLoc = "DEVOLUÇÕES-GERAL";
            if (dto.Grading == "A")
            {
                destinationLoc = "PICKING-ATIVO"; // Sellable stock
            }
            else if (dto.Grading == "B")
            {
                destinationLoc = "ZONA-VAS"; // VAS / Rework required
            }
            else if (dto.Grading == "C")
            {
                destinationLoc = "QUARENTENA-SUCATA"; // Damaged
            }

            linha.DestinoLocalizacao = destinationLoc;

            // Adjust physical stock in database
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.Ref == linha.Ref && s.Armazem == 1 && s.Localizacao == destinationLoc);

            if (stock == null)
            {
                stock = new Stock
                {
                    Ref = linha.Ref,
                    Armazem = 1,
                    Localizacao = destinationLoc,
                    Quantidade = linha.Quantidade,
                    PhcStamp = "RMA-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Stocks.Add(stock);
            }
            else
            {
                stock.Quantidade += linha.Quantidade;
                stock.UpdatedAt = DateTime.UtcNow;
            }

            rma.Status = "Inspecionada";
            rma.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"Inspeção gravada. Artigo reencaminhado para {destinationLoc}.", Destino = destinationLoc });
        }

        [HttpPost("rma/settle")]
        public async Task<IActionResult> SettleRma([FromBody] RmaSettleDto dto)
        {
            var rma = await _context.Rmas
                .Include(r => r.Linhas)
                .FirstOrDefaultAsync(r => r.Id == dto.RmaId);

            if (rma == null) return NotFound(new { Message = "RMA não encontrada." });

            rma.Status = "Concluida";
            rma.UpdatedAt = DateTime.UtcNow;

            // Reconcile values for webhook
            decimal calculatedEur = 0;
            var inspectedItems = new List<object>();

            foreach (var l in rma.Linhas)
            {
                // Retrieve price if exist
                var product = await _context.Produtos.FirstOrDefaultAsync(p => p.Ref == l.Ref);
                decimal unitPrice = 1.0m; // Mock unit price
                calculatedEur += l.Quantidade * unitPrice;

                inspectedItems.Add(new
                {
                    sku = l.Ref,
                    qty = l.Quantidade,
                    grade = l.Grading,
                    destination = l.DestinoLocalizacao
                });
            }

            // Webhook payload to ERP
            var webhookPayload = new
            {
                @event = "rma.inspection_completed",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                rma_id = rma.RmaCodigo,
                original_invoice_ref = rma.InvoiceRef,
                customer_id = "PT" + rma.ClienteNo,
                warehouse_source_id = "WH_LISBON_01",
                items_inspected = inspectedItems,
                financial_action_required = "GENERATE_CREDIT_NOTE",
                calculated_refund_subtotal_eur = calculatedEur
            };

            // Trigger ERP Webhook async
            await TriggerRmaWebhookAsync(webhookPayload);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "RMA concluída com sucesso. Webhook de notificação financeira enviado para o ERP.",
                WebhookSent = webhookPayload
            });
        }

        private async Task TriggerRmaWebhookAsync(object payload)
        {
            try
            {
                string webhookUrl = "http://localhost:5000/api/mock-erp-webhook";
                var json = JsonSerializer.Serialize(payload);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(webhookUrl, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Webhook notification simulated (receiver not listening): {ex.Message}");
            }
        }
    }
}
