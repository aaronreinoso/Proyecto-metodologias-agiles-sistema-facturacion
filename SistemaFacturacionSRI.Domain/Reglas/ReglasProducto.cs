using System.Collections.Generic;

namespace SistemaFacturacionSRI.Domain.Reglas
{
    // Esta clase simula las reglas de negocio del Wizard (UX)
    public static class ReglasProducto
    {
        // IDs de TarifaIva (Deben coincidir con el Seed Data de AppDbContext)
        public const int IVA_0_ID = 1;
        public const int IVA_5_ID = 4;
        public const int IVA_15_ID = 3;

        public class CategoriaSimulada
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string TipoAgrupacion { get; set; } = string.Empty; // Ayuda a la lógica
            public int TarifaIvaIdPorDefecto { get; set; }
            public bool EsPereciblePorDefecto { get; set; }
            public bool PermiteCambioIVA { get; set; } // ¿Permite al usuario cambiar el IVA?
            public string DescripcionRegla { get; set; } = string.Empty; // Para notas en la UI
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
                PermiteCambioIVA = false,
                DescripcionRegla = "Bloqueado a 0%. Permite elegir Caducidad (Leche) o No (Libros)."
            },
            /*new CategoriaSimulada
            {
                Id = 2,
                Nombre = "II. Construcción (Materiales al 5%)",
                TipoAgrupacion = "Construccion",
                TarifaIvaIdPorDefecto = IVA_5_ID,
                EsPereciblePorDefecto = false,
                PermiteCambioIVA = false,
                DescripcionRegla = "Bloqueado a 5%. Fijo como No Perecible."
            },*/
            new CategoriaSimulada
            {
                Id = 3,
                Nombre = "III. Otros (Consumo General)",
                TipoAgrupacion = "General",
                TarifaIvaIdPorDefecto = IVA_15_ID,
                EsPereciblePorDefecto = false,
                PermiteCambioIVA = true,
                DescripcionRegla = "IVA 15% por defecto. Permite elegir Caducidad o No."
            }
        };

        public static CategoriaSimulada? GetReglaById(int id)
        {
            return CategoriasBase.Find(c => c.Id == id);
        }
    }
}