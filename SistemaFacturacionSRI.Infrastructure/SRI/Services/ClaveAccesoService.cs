using System;
using System.Linq;
using System.Text;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Services
{
    public class ClaveAccesoService
    {
        /// <summary>
        /// Genera la clave de acceso de 49 dígitos requerida por el SRI.
        /// </summary>
        /// <param name="fechaEmision">Fecha de emisión de la factura</param>
        /// <param name="tipoComprobante">"01" para Factura, "07" Retención, etc.</param>
        /// <param name="ruc">RUC del emisor (13 dígitos)</param>
        /// <param name="ambiente">"1" Pruebas, "2" Producción</param>
        /// <param name="establecimiento">Código de establecimiento (ej: "001")</param>
        /// <param name="puntoEmision">Código punto de emisión (ej: "002")</param>
        /// <param name="secuencial">Número de factura (ej: "123")</param>
        /// <param name="codigoNumerico">Número aleatorio de 8 dígitos generado por el sistema</param>
        /// <param name="tipoEmision">"1" para Emisión Normal (Por defecto)</param>
        /// <returns>Clave de acceso de 49 dígitos</returns>
        public string GenerarClaveAcceso(
            DateTime fechaEmision,
            string tipoComprobante,
            string ruc,
            string ambiente,
            string establecimiento,
            string puntoEmision,
            string secuencial,
            string codigoNumerico,
            string tipoEmision = "1")
        {
            // 1. Validaciones y Formateo (Padding) para asegurar longitud exacta
            string fecha = fechaEmision.ToString("ddMMyyyy"); // 8 dígitos
            string serie = (establecimiento + puntoEmision).Replace("-", ""); // Debe sumar 6 dígitos

            // Rellenar con ceros a la izquierda si faltan caracteres
            // Estructura: Fecha(8) + Tipo(2) + RUC(13) + Ambiente(1) + Serie(6) + Secuencial(9) + CodigoNum(8) + TipoEmi(1)
            // Total base: 48 dígitos

            var sb = new StringBuilder();
            sb.Append(fecha);                                       // 8
            sb.Append(tipoComprobante);                             // 2
            sb.Append(ruc.PadRight(13));                            // 13 (Asegurar 13 chars)
            sb.Append(ambiente);                                    // 1
            sb.Append(serie.PadLeft(6, '0'));                       // 6
            sb.Append(secuencial.PadLeft(9, '0'));                  // 9
            sb.Append(codigoNumerico.PadLeft(8, '0'));              // 8
            sb.Append(tipoEmision);                                 // 1

            string claveGenerada = sb.ToString();

            // Validación de seguridad: La clave base debe tener 48 caracteres
            if (claveGenerada.Length != 48)
            {
                throw new InvalidOperationException($"La clave de acceso base no tiene 48 dígitos. Tiene {claveGenerada.Length}. Verifique los datos de entrada.");
            }

            // 2. Calcular Dígito Verificador (Módulo 11)
            string digitoVerificador = CalcularModulo11(claveGenerada);

            // 3. Retornar Clave de Acceso Completa (49 dígitos)
            return claveGenerada + digitoVerificador;
        }

        private string CalcularModulo11(string clave48)
        {
            // Algoritmo Módulo 11 SRI:
            // Se multiplica cada dígito por un factor (2, 3, 4, 5, 6, 7) de DERECHA a IZQUIERDA.
            // Si el factor pasa de 7, se reinicia a 2.

            int[] claveArray = clave48.Select(c => int.Parse(c.ToString())).ToArray();
            int factor = 2;
            int suma = 0;

            // Recorrer de derecha a izquierda
            for (int i = claveArray.Length - 1; i >= 0; i--)
            {
                suma += claveArray[i] * factor;
                factor++;
                if (factor > 7) factor = 2;
            }

            int residuo = suma % 11;
            int digito = 11 - residuo;

            if (digito == 11) return "0";
            if (digito == 10) return "1";

            return digito.ToString();
        }
    }
}