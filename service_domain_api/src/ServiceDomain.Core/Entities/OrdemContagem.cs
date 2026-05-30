using System;
using System.Collections.Generic;

namespace ServiceDomain.Core.Entities
{
    public class OrdemContagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TipoContagem { get; set; } = "ABC"; // ABC, GEOGRAFICA, EXCECAO
        public string Estado { get; set; } = "PENDENTE"; // PENDENTE, EM_RECONTAGEM, CONCLUIDO
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public string? SupervisorId { get; set; }

        public ICollection<LinhaContagem> Linhas { get; set; } = new List<LinhaContagem>();
    }
}
