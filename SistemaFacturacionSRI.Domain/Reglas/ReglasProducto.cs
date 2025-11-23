using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;

namespace SistemaFacturacionSRI.Domain.Reglas
{
    // Clase que contiene las reglas de negocio fijas para el Wizard de Creación.
    public static class ReglasProducto
    {
        // IDs de las TarifaIva: 1=0%, 3=15%, 4=5% (Basado en el seed de AppDbContext)
        // La Tarifa 12% (ID 2) y 8% (ID 5) están disponibles en la BD pero no se usan en este Wizard simplificado.

        public const int IVA_0_ID = 1;
        public const int IVA_5_ID = 4;
        public const int IVA_15_ID = 3;

        // Estructura para definir la lógica del Wizard
        public class CategoriaSimulada
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string TipoAgrupacion { get; set; } = string.Empty;
            public int TarifaIvaIdPorDefecto { get; set; }
            public bool EsPereciblePorDefecto { get; set; }
            public bool PermiteCambioIVA { get; set; }
            public bool PermiteCambioPerecible { get; set; }
        }

        public static List<CategoriaSimulada> CategoriasBase = new List<CategoriaSimulada>
        {
            new CategoriaSimulada
            {
                Id = 1,
                Nombre = "I. Canasta Básica / Medicinas",
                TipoAgrupacion = "Canasta",
                TarifaIvaIdPorDefecto = IVA_0_ID,
                EsPereciblePorDefecto = true,
                PermiteCambioIVA = false, // Bloqueado a 0%
                PermiteCambioPerecible = true // Puede ser Con Caducidad (Leche) o Sin (Libros)
            },
            new CategoriaSimulada
            {
                Id = 2,
                Nombre = "II. Construcción (Materiales Generales 15%)",
                TipoAgrupacion = "Construccion",
                TarifaIvaIdPorDefecto = IVA_15_ID,
                EsPereciblePorDefecto = false,
                PermiteCambioIVA = false, // Lo dejamos en 15% por simplicidad
                PermiteCambioPerecible = false
            },
            new CategoriaSimulada
            {
                Id = 3,
                Nombre = "III. Otros (Consumo General)",
                TipoAgrupacion = "General",
                TarifaIvaIdPorDefecto = IVA_15_ID,
                EsPereciblePorDefecto = false,
                PermiteCambioIVA = true, // Permite 0% o 15%
                PermiteCambioPerecible = true
            }
        };

        // Función auxiliar para buscar tarifas
        public static CategoriaSimulada? GetReglaById(int id)
        {
            return CategoriasBase.Find(c => c.Id == id);
        }
    }
}
