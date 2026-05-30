using System.ComponentModel.DataAnnotations;

namespace ServiceDomain.Api.Models
{
    public class StockMovementDto
    {
        [Required]
        [StringLength(50)]
        public string Ref { get; set; } = string.Empty;

        [StringLength(100)]
        public string? LoteCodigo { get; set; }

        [Required]
        public int Armazem { get; set; }

        [Required]
        [StringLength(50)]
        public string Localizacao { get; set; } = string.Empty;

        [Required]
        public decimal Quantidade { get; set; } // Positive for addition, negative for deduction
    }
}
