using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ServiceDomain.Api.Models
{
    public class PickingDto
    {
        [Required]
        public Guid EncomendaId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "A guia de picking deve ter pelo menos uma linha de recolha.")]
        public List<PickingLinhaDto> Linhas { get; set; } = new List<PickingLinhaDto>();
    }

    public class PickingLinhaDto
    {
        [Required]
        public Guid LinhaId { get; set; }

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "A quantidade recolhida deve ser superior a zero.")]
        public decimal QuantidadeRecolhida { get; set; }

        [StringLength(100)]
        public string? Lote { get; set; }

        [Required]
        [StringLength(50)]
        public string Localizacao { get; set; } = string.Empty;
    }

    public class RececaoDto
    {
        [Required]
        public Guid EncomendaId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "A guia de receção deve ter pelo menos uma linha de artigo.")]
        public List<RececaoLinhaDto> Linhas { get; set; } = new List<RececaoLinhaDto>();
    }

    public class RececaoLinhaDto
    {
        [Required]
        public Guid LinhaId { get; set; }

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "A quantidade recebida deve ser superior a zero.")]
        public decimal QuantidadeRecebida { get; set; }

        [StringLength(100)]
        public string? Lote { get; set; }

        [Required]
        [StringLength(50)]
        public string Localizacao { get; set; } = string.Empty; // Warehouse shelf location
    }
}
