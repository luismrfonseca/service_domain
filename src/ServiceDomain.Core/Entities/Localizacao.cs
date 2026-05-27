using System;

namespace ServiceDomain.Core.Entities
{
    public class Localizacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Armazem { get; set; }                       // arm.no in PHC
        public string Nome { get; set; } = string.Empty;       // arm.nome in PHC
        public string PhcStamp { get; set; } = string.Empty;   // arm.armstamp in PHC
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
