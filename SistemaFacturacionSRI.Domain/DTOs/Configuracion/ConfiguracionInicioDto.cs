using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Configuracion
{
    public class ConfiguracionInicioDto
    {
        [Required(ErrorMessage = "El RUC es obligatorio")]
        public string Ruc { get; set; } = "";

        [Required(ErrorMessage = "La Razón Social es obligatoria")]
        public string RazonSocial { get; set; } = "";

        public string? NombreComercial { get; set; }

        [Required(ErrorMessage = "La Dirección Matriz es obligatoria")]
        public string DireccionMatriz { get; set; } = "";

        public string? DireccionEstablecimiento { get; set; }

        public string CodigoEstablecimiento { get; set; } = "001";

        public string CodigoPuntoEmision { get; set; } = "001";

        public bool ObligadoContabilidad { get; set; }

        // --- CAMPOS QUE FALTABAN ---
        public string? ContribuyenteEspecial { get; set; } // Número de resolución

        public string? NombreArchivoFirma { get; set; }    // Para que el usuario sepa que ya subió una firma (ej: "firma.p12")

        public string? ClaveFirma { get; set; }
    }
}