using System;

namespace ServiceDomain.Core.Entities
{
    public class RmaLinha
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RmaId { get; set; }
        public string Ref { get; set; } = string.Empty;
        public decimal Quantidade { get; set; }
        public string? Grading { get; set; } // A, B, C
        public string? DestinoLocalizacao { get; set; }

        // Navigation properties
        public Rma Rma { get; set; } = null!;
    }
}
