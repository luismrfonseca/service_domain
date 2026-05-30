using System;

namespace ServiceDomain.Core.Entities
{
    public class Produto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Ref { get; set; } = string.Empty;        // st.ref in PHC
        public string Designacao { get; set; } = string.Empty; // st.design in PHC
        public string PhcStamp { get; set; } = string.Empty;   // st.ststamp in PHC
        public string Gtin { get; set; } = string.Empty;
        public decimal PesoUnitarioKg { get; set; }
        public decimal VolumeUnitarioM3 { get; set; }
        public char ClasseAbc { get; set; } = 'C';
        public bool RequerCq { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
