using System;

namespace ServiceDomain.Core.Entities
{
    public class LinhaContagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrdemId { get; set; }
        public Guid StockId { get; set; }
        public decimal QuantidadeSistema { get; set; }
        public decimal? QuantidadeContada1 { get; set; }
        public string? Operador1Id { get; set; }
        public decimal? QuantidadeContada2 { get; set; }
        public string? Operador2Id { get; set; }
        public DateTime? DataAprovacao { get; set; }
        public bool AjusteAplicado { get; set; }

        // Navigation properties
        public OrdemContagem Ordem { get; set; } = null!;
        public Stock Stock { get; set; } = null!;
    }
}
