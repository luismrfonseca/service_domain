using System;

namespace ServiceDomain.Core.Entities
{
    public class SyncOutbox
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EntityType { get; set; } = string.Empty; // e.g. "Encomenda", "Cliente"
        public Guid EntityId { get; set; }                     // Local ID of the entity
        public string Payload { get; set; } = string.Empty;    // JSON serialized data
        public string Status { get; set; } = "Pendente";        // Pendente, Processado, Erro
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}
