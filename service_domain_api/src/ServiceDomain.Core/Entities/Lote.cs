using System;

namespace ServiceDomain.Core.Entities
{
    public class Lote
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string LoteCodigo { get; set; } = string.Empty; // clot.lote in PHC
        public string Ref { get; set; } = string.Empty;        // Product Ref
        public string PhcStamp { get; set; } = string.Empty;   // clot.clotstamp in PHC
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
