using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;

namespace ServiceDomain.Worker.Workers
{
    public class InboxWorker : BackgroundService
    {
        private readonly ILogger<InboxWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _phcConnectionString;
        private const int PollingDelayMs = 5000; // Poll every 5 seconds

        public InboxWorker(
            ILogger<InboxWorker> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _phcConnectionString = configuration.GetConnectionString("PhcDbConnection")
                ?? throw new ArgumentNullException("PhcDbConnection connection string is missing.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Inbox Sync Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessInboxEventsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the Inbox process loop.");
                }

                await Task.Delay(PollingDelayMs, stoppingToken);
            }

            _logger.LogInformation("Inbox Sync Worker stopped.");
        }

        private async Task ProcessInboxEventsAsync(CancellationToken stoppingToken)
        {
            // Connect to PHC CS SQL Server to query pending events in u_phc_sync_queue
            using var phcConnection = new SqlConnection(_phcConnectionString);
            await phcConnection.OpenAsync(stoppingToken);

            // Fetch a batch of pending synchronization events
            var fetchSql = @"
                SELECT TOP 20 id, entity_type, phc_stamp, operation_type 
                FROM u_phc_sync_queue 
                WHERE status = 'Pendente' 
                ORDER BY created_at ASC";

            using var cmd = new SqlCommand(fetchSql, phcConnection);
            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);

            var batchList = new System.Collections.Generic.List<(int Id, string EntityType, string PhcStamp, string OpType)>();
            while (await reader.ReadAsync(stoppingToken))
            {
                batchList.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)
                ));
            }
            reader.Close();

            if (!batchList.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending changes in ERP PHC.", batchList.Count);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceDomainDbContext>();

            foreach (var evt in batchList)
            {
                if (stoppingToken.IsCancellationRequested) break;

                // Update event status to 'Processando' in PHC
                string updateStatusSql = "UPDATE u_phc_sync_queue SET status = 'Processando' WHERE id = @Id";
                using (var updateCmd = new SqlCommand(updateStatusSql, phcConnection))
                {
                    updateCmd.Parameters.AddWithValue("@Id", evt.Id);
                    await updateCmd.ExecuteNonQueryAsync(stoppingToken);
                }

                try
                {
                    // Apply change to local database
                    await ApplyPhcChangeLocallyAsync(dbContext, phcConnection, evt.EntityType, evt.PhcStamp, evt.OpType, stoppingToken);

                    // Mark as 'Processado' in PHC
                    string markProcessedSql = "UPDATE u_phc_sync_queue SET status = 'Processado', processed_at = @Now, error_message = NULL WHERE id = @Id";
                    using (var processedCmd = new SqlCommand(markProcessedSql, phcConnection))
                    {
                        processedCmd.Parameters.AddWithValue("@Id", evt.Id);
                        processedCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        await processedCmd.ExecuteNonQueryAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply PHC change localy for event id {Id} ({Type}).", evt.Id, evt.EntityType);

                    // Mark as 'Erro' in PHC with error trace
                    string markErrorSql = "UPDATE u_phc_sync_queue SET status = 'Erro', error_message = @Error WHERE id = @Id";
                    using (var errorCmd = new SqlCommand(markErrorSql, phcConnection))
                    {
                        errorCmd.Parameters.AddWithValue("@Id", evt.Id);
                        errorCmd.Parameters.AddWithValue("@Error", ex.ToString());
                        await errorCmd.ExecuteNonQueryAsync(stoppingToken);
                    }
                }
            }
        }

        private async Task ApplyPhcChangeLocallyAsync(
            ServiceDomainDbContext dbContext,
            SqlConnection phcConnection,
            string entityType,
            string phcStamp,
            string operationType,
            CancellationToken stoppingToken)
        {
            if (operationType == "DELETE")
            {
                await HandleDeleteOperationAsync(dbContext, entityType, phcStamp, stoppingToken);
                return;
            }

            switch (entityType)
            {
                case "Produto":
                    await SyncProdutoFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                case "Lote":
                    await SyncLoteFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                case "Localizacao":
                    await SyncLocalizacaoFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                case "Cliente":
                    await SyncClienteFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                case "Stock":
                    await SyncStockFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                case "Encomenda":
                case "EncomendaFornecedor":
                    await SyncEncomendaFromPhcAsync(dbContext, phcConnection, phcStamp, stoppingToken);
                    break;
                default:
                    _logger.LogWarning("Entity type '{Type}' from PHC sync queue is not supported.", entityType);
                    break;
            }
        }

        private async Task HandleDeleteOperationAsync(ServiceDomainDbContext dbContext, string entityType, string phcStamp, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Processing DELETE for {Type} with stamp {Stamp} locally...", entityType, phcStamp);

            if (entityType == "Produto")
            {
                var prod = await dbContext.Produtos.FirstOrDefaultAsync(p => p.PhcStamp == phcStamp, stoppingToken);
                if (prod != null) dbContext.Produtos.Remove(prod);
            }
            else if (entityType == "Lote")
            {
                var lote = await dbContext.Lotes.FirstOrDefaultAsync(l => l.PhcStamp == phcStamp, stoppingToken);
                if (lote != null) dbContext.Lotes.Remove(lote);
            }
            else if (entityType == "Localizacao")
            {
                var loc = await dbContext.Localizacoes.FirstOrDefaultAsync(l => l.PhcStamp == phcStamp, stoppingToken);
                if (loc != null) dbContext.Localizacoes.Remove(loc);
            }
            else if (entityType == "Cliente")
            {
                var cli = await dbContext.Clientes.FirstOrDefaultAsync(c => c.PhcStamp == phcStamp, stoppingToken);
                if (cli != null) dbContext.Clientes.Remove(cli);
            }
            else if (entityType == "Encomenda" || entityType == "EncomendaFornecedor")
            {
                var enc = await dbContext.Encomendas.FirstOrDefaultAsync(e => e.PhcStamp == phcStamp, stoppingToken);
                if (enc != null) dbContext.Encomendas.Remove(enc);
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }

        private async Task SyncProdutoFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            string sql = "SELECT ref, design FROM st WITH (NOLOCK) WHERE ststamp = @Stamp";
            using var cmd = new SqlCommand(sql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
            if (await reader.ReadAsync(stoppingToken))
            {
                string refCode = reader.GetString(0);
                string design = reader.GetString(1);
                reader.Close();

                var localProd = await dbContext.Produtos.FirstOrDefaultAsync(p => p.PhcStamp == stamp, stoppingToken);
                if (localProd == null)
                {
                    localProd = new Produto { PhcStamp = stamp };
                    dbContext.Produtos.Add(localProd);
                }

                localProd.Ref = refCode;
                localProd.Designacao = design;
                localProd.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synced Product {Ref} from PHC.", refCode);
            }
        }

        private async Task SyncLoteFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            string sql = "SELECT lote, ref FROM clot WITH (NOLOCK) WHERE clotstamp = @Stamp";
            using var cmd = new SqlCommand(sql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
            if (await reader.ReadAsync(stoppingToken))
            {
                string loteCode = reader.GetString(0);
                string refCode = reader.GetString(1);
                reader.Close();

                var localLote = await dbContext.Lotes.FirstOrDefaultAsync(l => l.PhcStamp == stamp, stoppingToken);
                if (localLote == null)
                {
                    localLote = new Lote { PhcStamp = stamp };
                    dbContext.Lotes.Add(localLote);
                }

                localLote.LoteCodigo = loteCode;
                localLote.Ref = refCode;
                localLote.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synced Batch/Lote {Lote} (Ref: {Ref}) from PHC.", loteCode, refCode);
            }
        }

        private async Task SyncLocalizacaoFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            string sql = "SELECT no, nome FROM arm WITH (NOLOCK) WHERE armstamp = @Stamp";
            using var cmd = new SqlCommand(sql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
            if (await reader.ReadAsync(stoppingToken))
            {
                int no = reader.GetInt32(0);
                string nome = reader.GetString(1);
                reader.Close();

                var localLoc = await dbContext.Localizacoes.FirstOrDefaultAsync(l => l.PhcStamp == stamp, stoppingToken);
                if (localLoc == null)
                {
                    localLoc = new Localizacao { PhcStamp = stamp };
                    dbContext.Localizacoes.Add(localLoc);
                }

                localLoc.Armazem = no;
                localLoc.Nome = nome;
                localLoc.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synced Location/Warehouse No {No} from PHC.", no);
            }
        }

        private async Task SyncClienteFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            string sql = "SELECT no, nome, nif, email FROM cl WITH (NOLOCK) WHERE clstamp = @Stamp";
            using var cmd = new SqlCommand(sql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
            if (await reader.ReadAsync(stoppingToken))
            {
                int no = reader.GetInt32(0);
                string nome = reader.GetString(1);
                string nif = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                string email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                reader.Close();

                // DEDUPLICATION GATE: Check if this clstamp already exists localy
                var localCli = await dbContext.Clientes.FirstOrDefaultAsync(c => c.PhcStamp == stamp, stoppingToken);
                if (localCli == null)
                {
                    localCli = new Cliente { PhcStamp = stamp };
                    dbContext.Clientes.Add(localCli);
                }

                localCli.No = no;
                localCli.Nome = nome;
                localCli.NomeFiscal = nif;
                localCli.Email = string.IsNullOrWhiteSpace(email) ? null : email;
                localCli.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synced Cliente No {No} from PHC.", no);
            }
        }

        private async Task SyncStockFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            // Note: Replace with actual PHC stock table (e.g. st, stlote, or custom view)
            
            // For now, let's select from standard 'st' table as example
            string stSql = "SELECT ref, stock, ststamp FROM st WITH (NOLOCK) WHERE ststamp = @Stamp";
            using var cmd = new SqlCommand(stSql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
            if (await reader.ReadAsync(stoppingToken))
            {
                string refCode = reader.GetString(0);
                decimal qty = reader.GetDecimal(1);
                reader.Close();

                // Update stock for Warehouse 1, default location
                var localStock = await dbContext.Stocks
                    .FirstOrDefaultAsync(s => s.Ref == refCode && s.Armazem == 1 && s.Localizacao == "GERAL", stoppingToken);

                if (localStock == null)
                {
                    localStock = new Stock
                    {
                        Ref = refCode,
                        LoteCodigo = null,
                        Armazem = 1,
                        Localizacao = "GERAL",
                        PhcStamp = stamp
                    };
                    dbContext.Stocks.Add(localStock);
                }

                localStock.Quantidade = qty;
                localStock.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synced Stock for Product {Ref} (Qty: {Qty}) from PHC.", refCode, qty);
            }
        }

        private async Task SyncEncomendaFromPhcAsync(ServiceDomainDbContext dbContext, SqlConnection phcConnection, string stamp, CancellationToken stoppingToken)
        {
            // 1. Read header (including document name to distinguish sales vs. purchases)
            string boSql = "SELECT no, clno, total, data, nome FROM bo WITH (NOLOCK) WHERE bostamp = @Stamp";
            using var cmd = new SqlCommand(boSql, phcConnection);
            cmd.Parameters.AddWithValue("@Stamp", stamp);

            int docNo = 0;
            int clientNo = 0;
            decimal total = 0;
            DateTime data = DateTime.UtcNow;
            string nomeDocumento = string.Empty;
            bool found = false;

            using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
            {
                if (await reader.ReadAsync(stoppingToken))
                {
                    docNo = reader.GetInt32(0);
                    clientNo = reader.GetInt32(1);
                    total = reader.GetDecimal(2);
                    data = reader.GetDateTime(3);
                    nomeDocumento = reader.GetString(4);
                    found = true;
                }
            }

            if (!found) return;

            // Determine if Sales Order or Purchase Order
            var docTipo = (nomeDocumento.Contains("Fornecedor") || nomeDocumento.Contains("Compra"))
                ? DocumentoTipo.EncomendaFornecedor
                : DocumentoTipo.EncomendaCliente;

            // DEDUPLICATION GATE: Check if the order already exists localy by PhcStamp
            var localEnc = await dbContext.Encomendas
                .Include(e => e.Linhas)
                .FirstOrDefaultAsync(e => e.PhcStamp == stamp, stoppingToken);

            if (localEnc == null)
            {
                // Verify if it exists by DocumentoNo and Tipo (to handle mapping of locally created order that was just synced and got the Stamp)
                localEnc = await dbContext.Encomendas
                    .Include(e => e.Linhas)
                    .FirstOrDefaultAsync(e => e.DocumentoNo == docNo && e.ClienteNo == clientNo && e.Status == "PendenteSync" && e.Tipo == docTipo, stoppingToken);
                
                if (localEnc != null)
                {
                    // This was a local order synced to ERP. Update stamp and mark as Sincronizado
                    localEnc.PhcStamp = stamp;
                    localEnc.Status = "Sincronizado";
                    localEnc.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Deduplicated: Mapped local order id {Id} to PHC stamp {Stamp}.", localEnc.Id, stamp);
                    return;
                }

                // If not found anywhere, it's a NEW order created in PHC directly
                localEnc = new Encomenda
                {
                    PhcStamp = stamp,
                    Tipo = docTipo,
                    DocumentoNo = docNo,
                    ClienteNo = clientNo,
                    Total = total,
                    Data = data,
                    Status = "Sincronizado",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Encomendas.Add(localEnc);
            }
            else
            {
                // Order was updated in ERP PHC. Update local header details
                localEnc.Total = total;
                localEnc.Data = data;
                localEnc.Tipo = docTipo;
                localEnc.UpdatedAt = DateTime.UtcNow;

                // Remove existing lines to rebuild them
                dbContext.EncomendaLinhas.RemoveRange(localEnc.Linhas);
                localEnc.Linhas.Clear();
            }

            // 2. Read lines and rebuild/insert
            string biSql = "SELECT ref, design, qtt, preco, lote, bistamp FROM bi WITH (NOLOCK) WHERE bostamp = @Stamp";
            using var biCmd = new SqlCommand(biSql, phcConnection);
            biCmd.Parameters.AddWithValue("@Stamp", stamp);

            using (var biReader = await biCmd.ExecuteReaderAsync(stoppingToken))
            {
                while (await biReader.ReadAsync(stoppingToken))
                {
                    localEnc.Linhas.Add(new EncomendaLinha
                    {
                        Ref = biReader.GetString(0),
                        Designacao = biReader.IsDBNull(1) ? string.Empty : biReader.GetString(1),
                        Quantidade = biReader.GetDecimal(2),
                        Preco = biReader.GetDecimal(3),
                        Lote = biReader.IsDBNull(4) ? null : biReader.GetString(4),
                        PhcStamp = biReader.GetString(5)
                    });
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Synced Encomenda No {No} of type {Tipo} (Stamp: {Stamp}) from PHC.", docNo, docTipo, stamp);
        }
    }
}
