using System;

namespace ServiceDomain.Core.Entities
{
    public class Cliente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int No { get; set; }                             // cl.no in PHC
        public string Nome { get; set; } = string.Empty;       // cl.nome in PHC
        public string NomeFiscal { get; set; } = string.Empty; // cl.nif / vat number
        public string? Email { get; set; }
        public string PhcStamp { get; set; } = string.Empty;   // cl.clstamp in PHC
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
