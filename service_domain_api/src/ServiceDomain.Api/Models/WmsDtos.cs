using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ServiceDomain.Api.Models
{
    public class Gs1DecodeDto
    {
        [Required]
        public string RawBarcode { get; set; } = string.Empty;
    }

    public class Gs1ResultDto
    {
        public string? Gtin { get; set; }
        public string? ExpiryDate { get; set; }
        public string? Lot { get; set; }
        public int? Quantity { get; set; }
        public string? Raw { get; set; }
        public string? ValidationError { get; set; }
    }

    public class PutawaySuggestionDto
    {
        public string LocalizacaoId { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Corredor { get; set; } = string.Empty;
        public string Estante { get; set; } = string.Empty;
        public string Prateleira { get; set; } = string.Empty;
        public string Alveolo { get; set; } = string.Empty;
        public decimal MaxPesoKg { get; set; }
        public decimal MaxVolumeM3 { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ContagemCriarDto
    {
        [Required]
        public string TipoContagem { get; set; } = "ABC"; // ABC, GEOGRAFICA, EXCECAO

        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [Required]
        public List<Guid> StockIds { get; set; } = new List<Guid>();
    }

    public class ContagemRegistarDto
    {
        [Required]
        public Guid OrdemId { get; set; }

        [Required]
        public Guid LinhaId { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal QuantidadeContada { get; set; }

        [Required]
        public string OperadorId { get; set; } = string.Empty;
    }

    public class ContagemAprovarDto
    {
        [Required]
        public Guid OrdemId { get; set; }

        [Required]
        public string SupervisorId { get; set; } = string.Empty;
    }

    public class ValidatePackingWeightDto
    {
        [Required]
        public decimal ActualWeight { get; set; }

        [Required]
        public List<PackedItemDto> PackedItems { get; set; } = new List<PackedItemDto>();

        [Required]
        public decimal BoxTare { get; set; }

        public decimal TolerancePercent { get; set; } = 2.0m;
    }

    public class PackedItemDto
    {
        [Required]
        public string Ref { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantidade { get; set; }

        [Required]
        public decimal PesoUnitarioKg { get; set; }
    }

    public class RmaCriarDto
    {
        [Required]
        public string RmaCodigo { get; set; } = string.Empty;

        [Required]
        public string InvoiceRef { get; set; } = string.Empty;

        [Required]
        public int ClienteNo { get; set; }

        [Required]
        public List<RmaLinhaCriarDto> Linhas { get; set; } = new List<RmaLinhaCriarDto>();
    }

    public class RmaLinhaCriarDto
    {
        [Required]
        public string Ref { get; set; } = string.Empty;

        [Required]
        [Range(0.0001, double.MaxValue)]
        public decimal Quantidade { get; set; }
    }

    public class RmaGradeDto
    {
        [Required]
        public Guid RmaId { get; set; }

        [Required]
        public Guid LinhaId { get; set; }

        [Required]
        [RegularExpression("^[A-C]$", ErrorMessage = "O Grau de Classificação deve ser A, B ou C.")]
        public string Grading { get; set; } = string.Empty;
    }

    public class RmaSettleDto
    {
        [Required]
        public Guid RmaId { get; set; }
    }
}
