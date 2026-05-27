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
        }
    }
}
