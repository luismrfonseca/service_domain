using System;

namespace ServiceDomain.Core.Entities
{
    public class Stock
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Ref { get; set; } = string.Empty;
        public string? LoteCodigo { get; set; }
        public int Armazem { get; set; }
        public string Localizacao { get; set; } = string.Empty;
        public decimal Quantidade { get; set; }
        public string PhcStamp { get; set; } = string.Empty; // Maps to individual stock row or tracking key in PHC
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
