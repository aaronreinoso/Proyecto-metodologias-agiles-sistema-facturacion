using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.BlazorApp.DTOs.Tarifas
{
    // Este DTO sirve para listar las tarifas disponibles en los selectores del frontend
    public class TarifaIvaDto
    {
        public int Id { get; set; }
        public string CodigoSRI { get; set; } = string.Empty;
        public decimal Porcentaje { get; set; }

        // Propiedad de solo lectura para mostrar en el Select (ej: "15% (Código 4)")
        public string DescripcionVisual => $"{Porcentaje:N0}% (Código {CodigoSRI})";
    }
}
