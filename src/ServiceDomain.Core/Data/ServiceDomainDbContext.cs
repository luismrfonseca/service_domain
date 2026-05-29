using Microsoft.EntityFrameworkCore;
using ServiceDomain.Core.Entities;

namespace ServiceDomain.Core.Data
{
    public class ServiceDomainDbContext : DbContext
    {
        public ServiceDomainDbContext(DbContextOptions<ServiceDomainDbContext> options)
            : base(options)
        {
        }

        public DbSet<Produto> Produtos => Set<Produto>();
        public DbSet<Lote> Lotes => Set<Lote>();
        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<Localizacao> Localizacoes => Set<Localizacao>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Encomenda> Encomendas => Set<Encomenda>();
        public DbSet<EncomendaLinha> EncomendaLinhas => Set<EncomendaLinha>();
        public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
        public DbSet<SyncInbox> SyncInbox => Set<SyncInbox>();
        public DbSet<OrdemContagem> OrdensContagem => Set<OrdemContagem>();
        public DbSet<LinhaContagem> LinhasContagem => Set<LinhaContagem>();
        public DbSet<Rma> Rmas => Set<Rma>();
        public DbSet<RmaLinha> RmaLinhas => Set<RmaLinha>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Produto Configuration
            modelBuilder.Entity<Produto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ref).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Designacao).HasMaxLength(200);
                entity.Property(e => e.PhcStamp).HasMaxLength(25).IsRequired();
                entity.Property(e => e.Gtin).HasMaxLength(14);
                entity.Property(e => e.PesoUnitarioKg).HasPrecision(18, 4);
                entity.Property(e => e.VolumeUnitarioM3).HasPrecision(18, 6);

                entity.HasIndex(e => e.Ref).IsUnique();
                entity.HasIndex(e => e.PhcStamp).IsUnique();
            });

            // Lote Configuration
            modelBuilder.Entity<Lote>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LoteCodigo).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Ref).HasMaxLength(50).IsRequired();
                entity.Property(e => e.PhcStamp).HasMaxLength(25).IsRequired();

                entity.HasIndex(e => new { e.Ref, e.LoteCodigo }).IsUnique();
                entity.HasIndex(e => e.PhcStamp).IsUnique();
            });

            // Localizacao Configuration
            modelBuilder.Entity<Localizacao>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Armazem).IsRequired();
                entity.Property(e => e.Nome).HasMaxLength(100);
                entity.Property(e => e.PhcStamp).HasMaxLength(25).IsRequired();
                entity.Property(e => e.Zona).HasMaxLength(50);
                entity.Property(e => e.Corredor).HasMaxLength(20);
                entity.Property(e => e.Estante).HasMaxLength(20);
                entity.Property(e => e.Prateleira).HasMaxLength(20);
                entity.Property(e => e.Alveolo).HasMaxLength(20);
                entity.Property(e => e.MaxPesoKg).HasPrecision(18, 4);
                entity.Property(e => e.MaxVolumeM3).HasPrecision(18, 6);

                entity.HasIndex(e => e.Armazem).IsUnique();
                entity.HasIndex(e => e.PhcStamp).IsUnique();
            });

            // Stock Configuration
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ref).HasMaxLength(50).IsRequired();
                entity.Property(e => e.LoteCodigo).HasMaxLength(100);
                entity.Property(e => e.Localizacao).HasMaxLength(50);
                entity.Property(e => e.Quantidade).HasPrecision(18, 4);
                entity.Property(e => e.PhcStamp).HasMaxLength(25);

                entity.HasIndex(e => new { e.Ref, e.LoteCodigo, e.Armazem, e.Localizacao }).IsUnique();
                entity.HasIndex(e => e.PhcStamp);
            });

            // Cliente Configuration
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.No).IsRequired();
                entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
                entity.Property(e => e.NomeFiscal).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.PhcStamp).HasMaxLength(25).IsRequired();

                entity.HasIndex(e => e.No).IsUnique();
                entity.HasIndex(e => e.PhcStamp).IsUnique();
            });

            // Encomenda Configuration
            modelBuilder.Entity<Encomenda>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tipo).IsRequired();
                entity.Property(e => e.DocumentoNo).IsRequired();
                entity.Property(e => e.ClienteNo).IsRequired();
                entity.Property(e => e.Total).HasPrecision(18, 4);
                entity.Property(e => e.PhcStamp).HasMaxLength(25);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.ParentId);

                entity.HasIndex(e => new { e.DocumentoNo, e.Tipo }).IsUnique();
                entity.HasIndex(e => e.PhcStamp);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // EncomendaLinha Configuration
            modelBuilder.Entity<EncomendaLinha>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ref).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Designacao).HasMaxLength(200);
                entity.Property(e => e.Quantidade).HasPrecision(18, 4);
                entity.Property(e => e.Preco).HasPrecision(18, 4);
                entity.Property(e => e.Lote).HasMaxLength(100);
                entity.Property(e => e.Localizacao).HasMaxLength(50);
                entity.Property(e => e.PhcStamp).HasMaxLength(25);
                entity.Property(e => e.ParentLineId);

                entity.HasOne(d => d.Encomenda)
                    .WithMany(p => p.Linhas)
                    .HasForeignKey(d => d.EncomendaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.ParentLine)
                    .WithMany(p => p.ChildrenLines)
                    .HasForeignKey(d => d.ParentLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.PhcStamp);
            });

            // SyncOutbox Configuration
            modelBuilder.Entity<SyncOutbox>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Payload).IsRequired();

                entity.HasIndex(e => new { e.Status, e.CreatedAt });
            });

            // SyncInbox Configuration
            modelBuilder.Entity<SyncInbox>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.PhcStamp).HasMaxLength(25).IsRequired();
                entity.Property(e => e.Payload).IsRequired();

                entity.HasIndex(e => new { e.Status, e.CreatedAt });
            });

            // OrdemContagem Configuration
            modelBuilder.Entity<OrdemContagem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TipoContagem).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Estado).HasMaxLength(20).IsRequired();
                entity.Property(e => e.SupervisorId).HasMaxLength(50);

                entity.HasIndex(e => e.Estado);
            });

            // LinhaContagem Configuration
            modelBuilder.Entity<LinhaContagem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.QuantidadeSistema).HasPrecision(18, 4);
                entity.Property(e => e.QuantidadeContada1).HasPrecision(18, 4);
                entity.Property(e => e.QuantidadeContada2).HasPrecision(18, 4);
                entity.Property(e => e.Operador1Id).HasMaxLength(50);
                entity.Property(e => e.Operador2Id).HasMaxLength(50);

                entity.HasOne(d => d.Ordem)
                    .WithMany(p => p.Linhas)
                    .HasForeignKey(d => d.OrdemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Stock)
                    .WithMany()
                    .HasForeignKey(d => d.StockId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Rma Configuration
            modelBuilder.Entity<Rma>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RmaCodigo).HasMaxLength(30).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.InvoiceRef).HasMaxLength(50);

                entity.HasIndex(e => e.RmaCodigo).IsUnique();
                entity.HasIndex(e => e.Status);
            });

            // RmaLinha Configuration
            modelBuilder.Entity<RmaLinha>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ref).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Quantidade).HasPrecision(18, 4);
                entity.Property(e => e.Grading).HasMaxLength(10);
                entity.Property(e => e.DestinoLocalizacao).HasMaxLength(50);

                entity.HasOne(d => d.Rma)
                    .WithMany(p => p.Linhas)
                    .HasForeignKey(d => d.RmaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
