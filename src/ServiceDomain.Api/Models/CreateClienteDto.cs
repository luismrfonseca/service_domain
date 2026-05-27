using System.ComponentModel.DataAnnotations;

namespace ServiceDomain.Api.Models
{
    public class CreateClienteDto
    {
        [Required]
        [StringLength(200)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string NomeFiscal { get; set; } = string.Empty; // NIF / VAT Number

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
    }
}
