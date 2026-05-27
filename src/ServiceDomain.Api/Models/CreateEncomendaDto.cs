using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ServiceDomain.Api.Models
{
    public class CreateEncomendaDto
    {
        [Required]
        public int ClienteNo { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "A encomenda deve conter pelo menos uma linha.")]
        public List<EncomendaLinhaDto> Linhas { get; set; } = new List<EncomendaLinhaDto>();
    }

    public class EncomendaLinhaDto
    {
        [Required]
        [StringLength(50)]
        public string Ref { get; set; } = string.Empty;

        [StringLength(200)]
        public string Designacao { get; set; } = string.Empty;

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "A quantidade deve ser superior a zero.")]
        public decimal Quantidade { get; set; }

        [Required]
        [Range(0.0, double.MaxValue, ErrorMessage = "O preço não pode ser negativo.")]
        public decimal Preco { get; set; }

        [StringLength(100)]
        public string? Lote { get; set; }
    }
}
