using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class ConfiguracionSRI
    {
        [Key]
        public int Id { get; set; }

        // --- DATOS DEL EMISOR (NUEVO) ---
        [Required]
        [MaxLength(13)]
        public string Ruc { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string RazonSocial { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? NombreComercial { get; set; }

        [Required]
        [MaxLength(500)]
        public string DireccionMatriz { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? DireccionEstablecimiento { get; set; }

        [MaxLength(10)]
        public string CodigoEstablecimiento { get; set; } = "001";

        [MaxLength(10)]
        public string CodigoPuntoEmision { get; set; } = "001";

        public bool ObligadoContabilidad { get; set; } = false;

        [MaxLength(10)]
        public string? ContribuyenteEspecial { get; set; } // Número de resolución

        // --- FIRMA ELECTRÓNICA (YA LO TENÍAS) ---
        public byte[]? FirmaElectronica { get; set; } // Ahora es nullable porque puedes guardar los datos primero y la firma después
        public string? ClaveFirma { get; set; }
        public string? NombreArchivo { get; set; }
        public DateTime? FechaSubida { get; set; }
    }
}