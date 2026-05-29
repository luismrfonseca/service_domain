using System;
using System.Collections.Generic;

namespace ServiceDomain.Core.Entities
{
    public class Rma
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RmaCodigo { get; set; } = string.Empty; // RMA-YYYY-XXXXX
        public string Status { get; set; } = "Iniciada"; // Iniciada, Autorizada, Recebida, Inspecionada, Concluida
        public string InvoiceRef { get; set; } = string.Empty;
        public int ClienteNo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RmaLinha> Linhas { get; set; } = new List<RmaLinha>();
    }
}
