using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
    public class OutboxWorker : BackgroundService
    {
        private readonly ILogger<OutboxWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _phcConnectionString;
        private const int PollingDelayMs = 3000; // Poll every 3 seconds

        public OutboxWorker(
            ILogger<OutboxWorker> logger,
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
            _logger.LogInformation("Outbox Sync Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the Outbox process loop.");
                }

                await Task.Delay(PollingDelayMs, stoppingToken);
            }

            _logger.LogInformation("Outbox Sync Worker stopped.");
        }

        private async Task ProcessOutboxQueueAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceDomainDbContext>();

            // Fetch pending outbox messages ordered by creation time
            var pendingMessages = await dbContext.SyncOutbox
                .Where(x => x.Status == "Pendente" && x.RetryCount < 5)
                .OrderBy(x => x.CreatedAt)
                .Take(10) // Process in batches of 10
                .ToListAsync(stoppingToken);

            if (!pendingMessages.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending outbox messages to process.", pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                if (stoppingToken.IsCancellationRequested) break;

                message.Status = "Processando";
                await dbContext.SaveChangesAsync(stoppingToken);

                try
                {
                    // Execute the actual write to PHC SQL Server
                    await SyncToPhcDatabaseAsync(dbContext, message, stoppingToken);

                    message.Status = "Processado";
                    message.ProcessedAt = DateTime.UtcNow;
                    message.ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync Outbox message {Id} of type {Type}.", message.Id, message.EntityType);
                    
                    message.RetryCount++;
                    message.Status = message.RetryCount >= 5 ? "Erro" : "Pendente";
                    message.ErrorMessage = ex.ToString();
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }

        private async Task SyncToPhcDatabaseAsync(ServiceDomainDbContext dbContext, SyncOutbox message, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Syncing {Type} (Local ID: {EntityId}) to PHC...", message.EntityType, message.EntityId);

            // Connect directly to PHC CS SQL Server Database
            using var connection = new SqlConnection(_phcConnectionString);
            await connection.OpenAsync(stoppingToken);

            using var transaction = connection.BeginTransaction();

            try
            {
                if (message.EntityType == "Encomenda")
                {
                    await ProcessEncomendaSyncAsync(dbContext, connection, transaction, message, stoppingToken);
                }
                else if (message.EntityType == "Cliente")
                {
                    await ProcessClienteSyncAsync(dbContext, connection, transaction, message, stoppingToken);
                }
                else if (message.EntityType == "GuiaRemessa")
                {
                    await ProcessGuiaRemessaSyncAsync(dbContext, connection, transaction, message, stoppingToken);
                }
                else if (message.EntityType == "GuiaRececao")
                {
                    await ProcessGuiaRececaoSyncAsync(dbContext, connection, transaction, message, stoppingToken);
                }
                else
                {
                    throw new NotSupportedException($"Entity type '{message.EntityType}' is not supported for synchronization.");
                }

                await transaction.CommitAsync(stoppingToken);
            }
            catch
            {
                await transaction.RollbackAsync(stoppingToken);
                throw;
            }
        }

        private async Task ProcessEncomendaSyncAsync(ServiceDomainDbContext dbContext, SqlConnection connection, SqlTransaction transaction, SyncOutbox message, CancellationToken stoppingToken)
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;

            int clienteNo = root.GetProperty("ClienteNo").GetInt32();
            decimal total = root.GetProperty("Total").GetDecimal();
            string phcStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper(); 

            _logger.LogInformation("Processing SQL Direct insertion into PHC for Encomenda. Generated Stamp: {Stamp}", phcStamp);

            // 1. Read and update the document counter in PHC CS 'boconf' table
            int nextDocNo = 0;
            string getCounterSql = "SELECT TOP 1 obno FROM boconf WITH (UPDLOCK, ROWLOCK) WHERE obnome = 'Encomenda'";
            using (var cmd = new SqlCommand(getCounterSql, connection, transaction))
            {
                var result = await cmd.ExecuteScalarAsync(stoppingToken);
                nextDocNo = result != null ? Convert.ToInt32(result) + 1 : 1;
            }

            string updateCounterSql = "UPDATE boconf SET obno = @NextNo WHERE obnome = 'Encomenda'";
            using (var cmd = new SqlCommand(updateCounterSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@NextNo", nextDocNo);
                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 2. Insert header into 'bo'
            string insertHeaderSql = @"
                INSERT INTO bo (bo_stamp, no, obno, clno, total, data, usrdata, usrhora)
                VALUES (@Stamp, @No, @No, @ClienteNo, @Total, @Data, @UsrData, @UsrHora)";

            using (var cmd = new SqlCommand(insertHeaderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Stamp", phcStamp);
                cmd.Parameters.AddWithValue("@No", nextDocNo);
                cmd.Parameters.AddWithValue("@ClienteNo", clienteNo);
                cmd.Parameters.AddWithValue("@Total", total);
                cmd.Parameters.AddWithValue("@Data", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));

                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 3. Insert lines into 'bi'
            if (root.TryGetProperty("Linhas", out var linesElement))
            {
                int lineIndex = 1;
                foreach (var line in linesElement.EnumerateArray())
                {
                    string refCode = line.GetProperty("Ref").GetString() ?? string.Empty;
                    decimal qty = line.GetProperty("Quantidade").GetDecimal();
                    decimal price = line.GetProperty("Preco").GetDecimal();
                    string lineStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();
                    Guid localLineId = line.GetProperty("LocalLineId").GetGuid();

                    string insertLineSql = @"
                        INSERT INTO bi (bi_stamp, bo_stamp, ref, design, qtt, preco, usrdata, usrhora, line_no)
                        VALUES (@LineStamp, @BoStamp, @Ref, @Design, @Qty, @Price, @UsrData, @UsrHora, @LineNo)";

                    using (var cmd = new SqlCommand(insertLineSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@LineStamp", lineStamp);
                        cmd.Parameters.AddWithValue("@BoStamp", phcStamp);
                        cmd.Parameters.AddWithValue("@Ref", refCode);
                        cmd.Parameters.AddWithValue("@Design", line.TryGetProperty("Designacao", out var desEl) ? desEl.GetString() : "");
                        cmd.Parameters.AddWithValue("@Qty", qty);
                        cmd.Parameters.AddWithValue("@Price", price);
                        cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                        cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@LineNo", lineIndex++);

                        await cmd.ExecuteNonQueryAsync(stoppingToken);
                    }

                    // Update local line PhcStamp
                    var localLine = await dbContext.EncomendaLinhas.FindAsync(localLineId);
                    if (localLine != null)
                    {
                        localLine.PhcStamp = lineStamp;
                    }
                }
            }

            // Update local Encomenda with ERP info
            var localEnc = await dbContext.Encomendas.FindAsync(message.EntityId);
            if (localEnc != null)
            {
                localEnc.PhcStamp = phcStamp;
                localEnc.DocumentoNo = nextDocNo;
                localEnc.Status = "Sincronizado";
                localEnc.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully inserted Encomenda No {No} with stamp {Stamp} into PHC.", nextDocNo, phcStamp);
        }

        private async Task ProcessClienteSyncAsync(ServiceDomainDbContext dbContext, SqlConnection connection, SqlTransaction transaction, SyncOutbox message, CancellationToken stoppingToken)
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;

            string nome = root.GetProperty("Nome").GetString() ?? string.Empty;
            string nomeFiscal = root.TryGetProperty("NomeFiscal", out var nfEl) ? nfEl.GetString() ?? "" : "";
            string email = root.TryGetProperty("Email", out var emEl) ? emEl.GetString() ?? "" : "";
            string phcStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();

            _logger.LogInformation("Processing SQL Direct insertion into PHC for Cliente. Generated Stamp: {Stamp}", phcStamp);

            // Get next client number
            int nextCliNo = 1;
            string getCliCounterSql = "SELECT ISNULL(MAX(no), 0) + 1 FROM cl WITH (UPDLOCK, ROWLOCK)";
            using (var cmd = new SqlCommand(getCliCounterSql, connection, transaction))
            {
                var result = await cmd.ExecuteScalarAsync(stoppingToken);
                nextCliNo = Convert.ToInt32(result);
            }

            string insertCliSql = @"
                INSERT INTO cl (cl_stamp, no, nome, nif, email, usrdata, usrhora)
                VALUES (@Stamp, @No, @Nome, @Nif, @Email, @UsrData, @UsrHora)";

            using (var cmd = new SqlCommand(insertCliSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Stamp", phcStamp);
                cmd.Parameters.AddWithValue("@No", nextCliNo);
                cmd.Parameters.AddWithValue("@Nome", nome);
                cmd.Parameters.AddWithValue("@Nif", nomeFiscal);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));

                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // Update local Cliente
            var localCli = await dbContext.Clientes.FindAsync(message.EntityId);
            if (localCli != null)
            {
                localCli.PhcStamp = phcStamp;
                localCli.No = nextCliNo;
                localCli.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully inserted Cliente No {No} with stamp {Stamp} into PHC.", nextCliNo, phcStamp);
        }

        private async Task ProcessGuiaRemessaSyncAsync(ServiceDomainDbContext dbContext, SqlConnection connection, SqlTransaction transaction, SyncOutbox message, CancellationToken stoppingToken)
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;

            int clienteNo = root.GetProperty("ClienteNo").GetInt32();
            decimal total = root.GetProperty("Total").GetDecimal();
            string parentPhcStamp = root.GetProperty("ParentPhcStamp").GetString() ?? string.Empty;
            string phcStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();

            _logger.LogInformation("Processing SQL Direct insertion into PHC for Guia de Remessa. Generated Stamp: {Stamp}", phcStamp);

            // 1. Get next document number for Guia de Remessa from boconf
            int nextDocNo = 0;
            string getCounterSql = "SELECT TOP 1 obno FROM boconf WITH (UPDLOCK, ROWLOCK) WHERE obnome = 'Guia de Remessa'";
            using (var cmd = new SqlCommand(getCounterSql, connection, transaction))
            {
                var result = await cmd.ExecuteScalarAsync(stoppingToken);
                nextDocNo = result != null ? Convert.ToInt32(result) + 1 : 1;
            }

            string updateCounterSql = "UPDATE boconf SET obno = @NextNo WHERE obnome = 'Guia de Remessa'";
            using (var cmd = new SqlCommand(updateCounterSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@NextNo", nextDocNo);
                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 2. Insert into 'bo'
            string insertHeaderSql = @"
                INSERT INTO bo (bo_stamp, no, obno, clno, total, data, usrdata, usrhora, o_bostamp)
                VALUES (@Stamp, @No, @No, @ClienteNo, @Total, @Data, @UsrData, @UsrHora, @ParentBoStamp)";

            using (var cmd = new SqlCommand(insertHeaderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Stamp", phcStamp);
                cmd.Parameters.AddWithValue("@No", nextDocNo);
                cmd.Parameters.AddWithValue("@ClienteNo", clienteNo);
                cmd.Parameters.AddWithValue("@Total", total);
                cmd.Parameters.AddWithValue("@Data", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));
                cmd.Parameters.AddWithValue("@ParentBoStamp", parentPhcStamp);

                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 3. Insert lines into 'bi'
            if (root.TryGetProperty("Linhas", out var linesElement))
            {
                int lineIndex = 1;
                foreach (var line in linesElement.EnumerateArray())
                {
                    string refCode = line.GetProperty("Ref").GetString() ?? string.Empty;
                    decimal qty = line.GetProperty("Quantidade").GetDecimal();
                    decimal price = line.GetProperty("Preco").GetDecimal();
                    string lote = line.TryGetProperty("Lote", out var lEl) ? lEl.GetString() ?? "" : "";
                    string localizacao = line.TryGetProperty("Localizacao", out var locEl) ? locEl.GetString() ?? "" : "";
                    string parentLinePhcStamp = line.GetProperty("ParentLinePhcStamp").GetString() ?? string.Empty;
                    string lineStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();
                    Guid localLineId = line.GetProperty("LocalLineId").GetGuid();

                    string insertLineSql = @"
                        INSERT INTO bi (bi_stamp, bo_stamp, ref, design, qtt, preco, lote, armazem, usrdata, usrhora, line_no, o_bistamp, o_bostamp)
                        VALUES (@LineStamp, @BoStamp, @Ref, @Design, @Qty, @Price, @Lote, 1, @UsrData, @UsrHora, @LineNo, @ParentLineStamp, @ParentBoStamp)";

                    using (var cmd = new SqlCommand(insertLineSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@LineStamp", lineStamp);
                        cmd.Parameters.AddWithValue("@BoStamp", phcStamp);
                        cmd.Parameters.AddWithValue("@Ref", refCode);
                        cmd.Parameters.AddWithValue("@Design", line.TryGetProperty("Designacao", out var desEl) ? desEl.GetString() : "");
                        cmd.Parameters.AddWithValue("@Qty", qty);
                        cmd.Parameters.AddWithValue("@Price", price);
                        cmd.Parameters.AddWithValue("@Lote", lote);
                        cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                        cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@LineNo", lineIndex++);
                        cmd.Parameters.AddWithValue("@ParentLineStamp", parentLinePhcStamp);
                        cmd.Parameters.AddWithValue("@ParentBoStamp", parentPhcStamp);

                        await cmd.ExecuteNonQueryAsync(stoppingToken);
                    }

                    // Update local line PhcStamp
                    var localLine = await dbContext.EncomendaLinhas.FindAsync(localLineId);
                    if (localLine != null)
                    {
                        localLine.PhcStamp = lineStamp;
                    }
                }
            }

            // Update local Guia object
            var localGuia = await dbContext.Encomendas.FindAsync(message.EntityId);
            if (localGuia != null)
            {
                localGuia.PhcStamp = phcStamp;
                localGuia.DocumentoNo = nextDocNo;
                localGuia.Status = "Sincronizado";
                localGuia.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully inserted Guia de Remessa No {No} linked to Order Stamp {ParentStamp} in PHC.", nextDocNo, parentPhcStamp);
        }

        private async Task ProcessGuiaRececaoSyncAsync(ServiceDomainDbContext dbContext, SqlConnection connection, SqlTransaction transaction, SyncOutbox message, CancellationToken stoppingToken)
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;

            int vendorNo = root.GetProperty("ClienteNo").GetInt32();
            decimal total = root.GetProperty("Total").GetDecimal();
            string parentPhcStamp = root.GetProperty("ParentPhcStamp").GetString() ?? string.Empty;
            string phcStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();

            _logger.LogInformation("Processing SQL Direct insertion into PHC for Guia de Receção. Generated Stamp: {Stamp}", phcStamp);

            // 1. Get next document number for Guia de Receção from boconf
            int nextDocNo = 0;
            string getCounterSql = "SELECT TOP 1 obno FROM boconf WITH (UPDLOCK, ROWLOCK) WHERE obnome = 'Guia de Receção'";
            using (var cmd = new SqlCommand(getCounterSql, connection, transaction))
            {
                var result = await cmd.ExecuteScalarAsync(stoppingToken);
                nextDocNo = result != null ? Convert.ToInt32(result) + 1 : 1;
            }

            string updateCounterSql = "UPDATE boconf SET obno = @NextNo WHERE obnome = 'Guia de Receção'";
            using (var cmd = new SqlCommand(updateCounterSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@NextNo", nextDocNo);
                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 2. Insert into 'bo'
            string insertHeaderSql = @"
                INSERT INTO bo (bo_stamp, no, obno, clno, total, data, usrdata, usrhora, o_bostamp)
                VALUES (@Stamp, @No, @No, @VendorNo, @Total, @Data, @UsrData, @UsrHora, @ParentBoStamp)";

            using (var cmd = new SqlCommand(insertHeaderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Stamp", phcStamp);
                cmd.Parameters.AddWithValue("@No", nextDocNo);
                cmd.Parameters.AddWithValue("@VendorNo", vendorNo);
                cmd.Parameters.AddWithValue("@Total", total);
                cmd.Parameters.AddWithValue("@Data", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));
                cmd.Parameters.AddWithValue("@ParentBoStamp", parentPhcStamp);

                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // 3. Insert lines into 'bi'
            if (root.TryGetProperty("Linhas", out var linesElement))
            {
                int lineIndex = 1;
                foreach (var line in linesElement.EnumerateArray())
                {
                    string refCode = line.GetProperty("Ref").GetString() ?? string.Empty;
                    decimal qty = line.GetProperty("Quantidade").GetDecimal();
                    decimal price = line.GetProperty("Preco").GetDecimal();
                    string lote = line.TryGetProperty("Lote", out var lEl) ? lEl.GetString() ?? "" : "";
                    string localizacao = line.TryGetProperty("Localizacao", out var locEl) ? locEl.GetString() ?? "" : "";
                    string parentLinePhcStamp = line.GetProperty("ParentLinePhcStamp").GetString() ?? string.Empty;
                    string lineStamp = Guid.NewGuid().ToString("N").Substring(0, 25).ToUpper();
                    Guid localLineId = line.GetProperty("LocalLineId").GetGuid();

                    string insertLineSql = @"
                        INSERT INTO bi (bi_stamp, bo_stamp, ref, design, qtt, preco, lote, armazem, usrdata, usrhora, line_no, o_bistamp, o_bostamp)
                        VALUES (@LineStamp, @BoStamp, @Ref, @Design, @Qty, @Price, @Lote, 1, @UsrData, @UsrHora, @LineNo, @ParentLineStamp, @ParentBoStamp)";

                    using (var cmd = new SqlCommand(insertLineSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@LineStamp", lineStamp);
                        cmd.Parameters.AddWithValue("@BoStamp", phcStamp);
                        cmd.Parameters.AddWithValue("@Ref", refCode);
                        cmd.Parameters.AddWithValue("@Design", line.TryGetProperty("Designacao", out var desEl) ? desEl.GetString() : "");
                        cmd.Parameters.AddWithValue("@Qty", qty);
                        cmd.Parameters.AddWithValue("@Price", price);
                        cmd.Parameters.AddWithValue("@Lote", lote);
                        cmd.Parameters.AddWithValue("@UsrData", DateTime.Today);
                        cmd.Parameters.AddWithValue("@UsrHora", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@LineNo", lineIndex++);
                        cmd.Parameters.AddWithValue("@ParentLineStamp", parentLinePhcStamp);
                        cmd.Parameters.AddWithValue("@ParentBoStamp", parentPhcStamp);

                        await cmd.ExecuteNonQueryAsync(stoppingToken);
                    }

                    // Update local line PhcStamp
                    var localLine = await dbContext.EncomendaLinhas.FindAsync(localLineId);
                    if (localLine != null)
                    {
                        localLine.PhcStamp = lineStamp;
                    }
                }
            }

            // Update local Guia object
            var localGuia = await dbContext.Encomendas.FindAsync(message.EntityId);
            if (localGuia != null)
            {
                localGuia.PhcStamp = phcStamp;
                localGuia.DocumentoNo = nextDocNo;
                localGuia.Status = "Sincronizado";
                localGuia.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully inserted Guia de Receção No {No} linked to Supplier Order Stamp {ParentStamp} in PHC.", nextDocNo, parentPhcStamp);
        }
    }
}
