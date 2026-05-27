using System;

namespace ServiceDomain.Core.Entities
{
    public class SyncInbox
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EntityType { get; set; } = string.Empty; // e.g. "Produto", "Stock", "Cliente"
        public string PhcStamp { get; set; } = string.Empty;   // Stamp identifier from PHC
        public string Payload { get; set; } = string.Empty;    // JSON serialized change data
        public string Status { get; set; } = "Pendente";        // Pendente, Processado, Erro
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}
