using System;

namespace ServiceDomain.Core.Entities
{
    public class Produto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Ref { get; set; } = string.Empty;        // st.ref in PHC
        public string Designacao { get; set; } = string.Empty; // st.design in PHC
        public string PhcStamp { get; set; } = string.Empty;   // st.ststamp in PHC
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
