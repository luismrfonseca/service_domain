using System;

namespace ServiceDomain.Core.Entities
{
    public class Localizacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Armazem { get; set; }                       // arm.no in PHC
        public string Nome { get; set; } = string.Empty;       // arm.nome in PHC
        public string PhcStamp { get; set; } = string.Empty;   // arm.armstamp in PHC
        public string Zona { get; set; } = string.Empty;
        public string Corredor { get; set; } = string.Empty;
        public string Estante { get; set; } = string.Empty;
        public string Prateleira { get; set; } = string.Empty;
        public string Alveolo { get; set; } = string.Empty;
        public decimal MaxPesoKg { get; set; }
        public decimal MaxVolumeM3 { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
