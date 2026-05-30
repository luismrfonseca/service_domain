using System;
using System.Collections.Generic;

namespace ServiceDomain.Core.Entities
{
    public class EncomendaLinha
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EncomendaId { get; set; }
        public string Ref { get; set; } = string.Empty;        // Product reference
        public string Designacao { get; set; } = string.Empty; // Product description
        public decimal Quantidade { get; set; }
        public decimal Preco { get; set; }
        public string? Lote { get; set; }                      // Batch number if applicable
        public string? Localizacao { get; set; }                // Stock location (e.g. shelf/aisle)
        public string? PhcStamp { get; set; }                  // bi.bistamp in PHC (null if created locally and not yet synced)
        public Guid? ParentLineId { get; set; }                 // Links guide line to original order line

        // Navigation properties
        public Encomenda Encomenda { get; set; } = null!;
        public EncomendaLinha? ParentLine { get; set; }
        public ICollection<EncomendaLinha> ChildrenLines { get; set; } = new List<EncomendaLinha>();
    }
}
