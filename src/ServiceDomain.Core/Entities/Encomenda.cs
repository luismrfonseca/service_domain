using System;
using System.Collections.Generic;

namespace ServiceDomain.Core.Entities
{
    public class Encomenda
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DocumentoTipo Tipo { get; set; } = DocumentoTipo.EncomendaCliente;
        public int DocumentoNo { get; set; }                    // Document number (bo.obno or bo.no in PHC)
        public int ClienteNo { get; set; }                      // client/vendor identifier (cl.no)
        public DateTime Data { get; set; } = DateTime.UtcNow;
        public decimal Total { get; set; }
        public string? PhcStamp { get; set; }                   // bo.bostamp in PHC (null if created locally and not yet synced)
        public string Status { get; set; } = "PendenteSync";    // PendenteSync, Sincronizado, Erro
        public Guid? ParentId { get; set; }                     // Links a delivery note (Guia) to the parent order
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Encomenda? Parent { get; set; }
        public ICollection<Encomenda> Children { get; set; } = new List<Encomenda>();
        public ICollection<EncomendaLinha> Linhas { get; set; } = new List<EncomendaLinha>();
    }
}
