using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;

var builder = WebApplication.CreateBuilder(args);

// Check if SQL Server is online
bool useSqlServer = false;
var connectionString = builder.Configuration.GetConnectionString("LocalDbConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    try
    {
        var testBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 1 // 1 second timeout for fast fallback
        };
        using (var conn = new SqlConnection(testBuilder.ConnectionString))
        {
            conn.Open();
            useSqlServer = true;
        }
    }
    catch
    {
        // SQL Server is offline in this sandbox, fall back to InMemory
    }
}

builder.Services.AddDbContext<ServiceDomainDbContext>(options =>
{
    if (useSqlServer)
    {
        options.UseSqlServer(connectionString, b => b.MigrationsAssembly("ServiceDomain.Api"));
    }
    else
    {
        options.UseInMemoryDatabase("ServiceDomainDb");
    }
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed initial mock data for WMS operations when using InMemory database
if (!useSqlServer)
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ServiceDomainDbContext>();
        context.Database.EnsureCreated();
        
        // Seed default products
        if (!context.Produtos.Any())
        {
            context.Produtos.AddRange(
                new Produto { Ref = "SKU-99023", Designacao = "Snack Proteico de Amendoim", PesoUnitarioKg = 0.800m, VolumeUnitarioM3 = 0.002m, ClasseAbc = 'A', RequerCq = false, PhcStamp = "P01" },
                new Produto { Ref = "SKU-10024", Designacao = "Barra Energética de Aveia", PesoUnitarioKg = 0.350m, VolumeUnitarioM3 = 0.001m, ClasseAbc = 'B', RequerCq = false, PhcStamp = "P02" },
                new Produto { Ref = "SKU-50067", Designacao = "Suplemento Omega 3 Premium", PesoUnitarioKg = 1.200m, VolumeUnitarioM3 = 0.005m, ClasseAbc = 'A', RequerCq = true, PhcStamp = "P03" }
            );
            context.SaveChanges();
        }

        // Seed default localizations
        if (!context.Localizacoes.Any())
        {
            context.Localizacoes.AddRange(
                new Localizacao { Nome = "A-01-02-01", Zona = "A", Corredor = "1", Estante = "2", Prateleira = "1", Alveolo = "1", MaxPesoKg = 1000m, MaxVolumeM3 = 10m },
                new Localizacao { Nome = "A-01-04-02", Zona = "A", Corredor = "1", Estante = "4", Prateleira = "2", Alveolo = "1", MaxPesoKg = 1000m, MaxVolumeM3 = 10m },
                new Localizacao { Nome = "B-02-01-01", Zona = "B", Corredor = "2", Estante = "1", Prateleira = "1", Alveolo = "1", MaxPesoKg = 800m, MaxVolumeM3 = 8m },
                new Localizacao { Nome = "CQ-01-01-01", Zona = "CQ", Corredor = "1", Estante = "1", Prateleira = "1", Alveolo = "1", MaxPesoKg = 500m, MaxVolumeM3 = 5m },
                new Localizacao { Nome = "RECEÇÃO-A1", Zona = "GERAL", Corredor = "0", Estante = "0", Prateleira = "0", Alveolo = "0", MaxPesoKg = 5000m, MaxVolumeM3 = 50m }
            );
            context.SaveChanges();
        }

        // Seed default stock
        if (!context.Stocks.Any())
        {
            context.Stocks.AddRange(
                new Stock { Ref = "SKU-99023", LoteCodigo = "LOTE12345", Armazem = 1, Localizacao = "A-01-02-01", Quantidade = 150, PhcStamp = "ST01", UpdatedAt = DateTime.UtcNow },
                new Stock { Ref = "SKU-10024", LoteCodigo = "LOTE67890", Armazem = 1, Localizacao = "B-02-01-01", Quantidade = 80, PhcStamp = "ST02", UpdatedAt = DateTime.UtcNow }
            );
            context.SaveChanges();
        }

        // Seed pending orders (Purchase Order PO for Inbound, Sales Order SO for Picking)
        if (!context.Encomendas.Any())
        {
            // Supplier PO (Rececao)
            var po = new Encomenda
            {
                Tipo = DocumentoTipo.EncomendaFornecedor,
                Status = "Sincronizado",
                ClienteNo = 50001,
                Data = DateTime.UtcNow.AddDays(-2),
                DocumentoNo = 202601,
                PhcStamp = "PO-STAMP-2026",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            po.Linhas.Add(new EncomendaLinha { Ref = "SKU-99023", Designacao = "Snack Proteico de Amendoim", Quantidade = 150, Preco = 1.50m, PhcStamp = "POL-01" });
            po.Linhas.Add(new EncomendaLinha { Ref = "SKU-50067", Designacao = "Suplemento Omega 3 Premium (Requer CQ)", Quantidade = 50, Preco = 12.00m, PhcStamp = "POL-02" });

            // Client SO (Picking)
            var so = new Encomenda
            {
                Tipo = DocumentoTipo.EncomendaCliente,
                Status = "Sincronizado",
                ClienteNo = 10009,
                Data = DateTime.UtcNow.AddDays(-1),
                DocumentoNo = 1024,
                PhcStamp = "SO-STAMP-1024",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            so.Linhas.Add(new EncomendaLinha { Ref = "SKU-99023", Designacao = "Snack Proteico de Amendoim", Quantidade = 15, Preco = 2.50m, Localizacao = "A-01-02-01", Lote = "LOTE12345", PhcStamp = "SOL-01" });
            so.Linhas.Add(new EncomendaLinha { Ref = "SKU-10024", Designacao = "Barra Energética de Aveia", Quantidade = 5, Preco = 1.80m, Localizacao = "B-02-01-01", Lote = "LOTE67890", PhcStamp = "SOL-02" });

            context.Encomendas.AddRange(po, so);
            context.SaveChanges();
        }

        // Seed RMA devoluções
        if (!context.Rmas.Any())
        {
            var rma = new Rma
            {
                RmaCodigo = "RMA-2026-90451",
                InvoiceRef = "FT 2026/8940",
                ClienteNo = 509123456,
                Status = "Recebida",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            rma.Linhas.Add(new RmaLinha { Ref = "SKU-99023", Quantidade = 3 });
            context.Rmas.Add(rma);
            context.SaveChanges();
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
