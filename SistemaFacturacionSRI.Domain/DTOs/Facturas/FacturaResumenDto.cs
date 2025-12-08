using System;

namespace SistemaFacturacionSRI.Domain.DTOs.Facturas
{
    public class FacturaResumenDto
    {
        public int Id { get; set; }
        public string NumeroFactura { get; set; } = string.Empty; // Ej: 001-001-000000123
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public DateTime FechaEmision { get; set; }
        public decimal Total { get; set; }

        // Datos del SRI
        public string EstadoSRI { get; set; } = "PENDIENTE";
        public string MensajeErrorSRI { get; set; } = string.Empty; // Para mostrar el error
        public string ClaveAcceso { get; set; } = string.Empty;
        public bool TieneXml { get; set; }
    }
}