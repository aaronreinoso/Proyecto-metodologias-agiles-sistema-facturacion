using SistemaFacturacionSRI.Domain.Enumss;
using System.Text.RegularExpressions;

namespace SistemaFacturacionSRI.Domain.Validations
{
    public static class ValidadorCliente
    {
        // 1. Definimos una clase simple para el resultado
        // Esto soluciona el error CS1061 inmediatamente.
        public class ResultadoValidacion
        {
            public bool EsValido { get; set; }
            // SOLUCIÓN: Inicializamos con cadena vacía para evitar el error CS8618
            public string Mensaje { get; set; } = string.Empty;

            public static ResultadoValidacion Ok() => new ResultadoValidacion { EsValido = true, Mensaje = "" };
            public static ResultadoValidacion Error(string msg) => new ResultadoValidacion { EsValido = false, Mensaje = msg };
        }

        // 2. Cambiamos el tipo de retorno de (bool, string) a ResultadoValidacion
        public static ResultadoValidacion ValidarIdentificacion(TipoIdentificacion tipo, string identificacion)
        {
            if (string.IsNullOrWhiteSpace(identificacion)) return ResultadoValidacion.Error("La identificación es obligatoria.");

            identificacion = identificacion.Trim();

            if (identificacion == "9999999999999") return ResultadoValidacion.Ok(); // Consumidor Final

            switch (tipo)
            {
                case TipoIdentificacion.Cedula:
                    return ValidarCedula(identificacion);
                case TipoIdentificacion.Ruc:
                    return ValidarRuc(identificacion);
                case TipoIdentificacion.Pasaporte:
                    return ValidarPasaporte(identificacion);
                default:
                    return ResultadoValidacion.Error("Tipo de identificación no válido.");
            }
        }

        private static ResultadoValidacion ValidarCedula(string cedula)
        {
            if (cedula.Length != 10 || !long.TryParse(cedula, out _))
                return ResultadoValidacion.Error("La cédula debe tener 10 dígitos numéricos.");

            if (!ValidarProvincia(cedula.Substring(0, 2)))
                return ResultadoValidacion.Error("Código de provincia inválido.");

            int tercerDigito = int.Parse(cedula.Substring(2, 1));
            if (tercerDigito >= 6) return ResultadoValidacion.Error("Tercer dígito inválido para cédula.");

            return ValidarModulo10(cedula) ? ResultadoValidacion.Ok() : ResultadoValidacion.Error("La cédula ingresada es inválida.");
        }

        private static ResultadoValidacion ValidarRuc(string ruc)
        {
            if (ruc.Length != 13 || !long.TryParse(ruc, out _))
                return ResultadoValidacion.Error("El RUC debe tener 13 dígitos numéricos.");

            if (!ValidarProvincia(ruc.Substring(0, 2)))
                return ResultadoValidacion.Error("Código de provincia del RUC inválido.");

            if (ruc.Substring(10, 3) == "000")
                return ResultadoValidacion.Error("El código de sucursal no puede ser 000.");

            int tercerDigito = int.Parse(ruc.Substring(2, 1));

            if (tercerDigito < 6) // Natural
            {
                return ValidarModulo10(ruc.Substring(0, 10)) ? ResultadoValidacion.Ok() : ResultadoValidacion.Error("RUC de Persona Natural inválido.");
            }
            else if (tercerDigito == 6) // Público
            {
                return ValidarModulo11(ruc, new int[] { 3, 2, 7, 6, 5, 4, 3, 2 }, 8) ? ResultadoValidacion.Ok() : ResultadoValidacion.Error("RUC Público inválido.");
            }
            else if (tercerDigito == 9) // Privado
            {
                return ValidarModulo11(ruc, new int[] { 4, 3, 2, 7, 6, 5, 4, 3, 2 }, 9) ? ResultadoValidacion.Ok() : ResultadoValidacion.Error("RUC Jurídico/Extranjero inválido.");
            }

            return ResultadoValidacion.Error("El tercer dígito del RUC es inválido.");
        }

        private static ResultadoValidacion ValidarPasaporte(string pasaporte)
        {
            if (pasaporte.Length < 3 || pasaporte.Length > 20)
                return ResultadoValidacion.Error("El pasaporte debe tener entre 3 y 20 caracteres.");

            if (!Regex.IsMatch(pasaporte, "^[a-zA-Z0-9]+$"))
                return ResultadoValidacion.Error("El pasaporte solo puede contener letras y números.");

            return ResultadoValidacion.Ok();
        }

        // --- VALIDACIONES DE DATOS ---

        public static ResultadoValidacion ValidarCorreo(string emailsInput)
        {
            if (string.IsNullOrWhiteSpace(emailsInput)) return ResultadoValidacion.Error("El correo es obligatorio.");

            var listaCorreos = emailsInput.Replace(";", ",").Split(',');
            var regex = new Regex(@"^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

            foreach (var item in listaCorreos)
            {
                var email = item.Trim();
                if (string.IsNullOrEmpty(email)) continue;

                if (!regex.IsMatch(email))
                    return ResultadoValidacion.Error($"El formato del correo '{email}' es inválido.");

                if (email.Contains("@gmil.com") || email.Contains("@hotmal.com") || email.Contains("@outlok.com"))
                    return ResultadoValidacion.Error($"Error en '{email}'. Verifique el dominio.");
            }

            return ResultadoValidacion.Ok();
        }

        public static ResultadoValidacion ValidarDireccion(string direccion)
        {
            if (string.IsNullOrWhiteSpace(direccion)) return ResultadoValidacion.Error("La dirección es obligatoria.");
            direccion = direccion.Trim();

            if (direccion.Length < 2) return ResultadoValidacion.Error("La dirección es muy corta.");
            if (direccion.Length > 300) return ResultadoValidacion.Error("La dirección excede los 300 caracteres.");

            if (direccion.Contains("\n") || direccion.Contains("\r"))
                return ResultadoValidacion.Error("La dirección no puede tener saltos de línea.");

            return ResultadoValidacion.Ok();
        }

        // --- Helpers Matemáticos (Privados) ---

        private static bool ValidarProvincia(string codigo)
        {
            int provincia = int.Parse(codigo);
            return (provincia >= 1 && provincia <= 24) || provincia == 30;
        }

        private static bool ValidarModulo10(string digitos)
        {
            int[] coeficientes = { 2, 1, 2, 1, 2, 1, 2, 1, 2 };
            int total = 0;
            int verificador = int.Parse(digitos.Substring(9, 1));

            for (int i = 0; i < 9; i++)
            {
                int valor = int.Parse(digitos.Substring(i, 1)) * coeficientes[i];
                total += (valor >= 10) ? valor - 9 : valor;
            }

            int digitoCalculado = (total % 10 == 0) ? 0 : 10 - (total % 10);
            return digitoCalculado == verificador;
        }

        private static bool ValidarModulo11(string ruc, int[] coeficientes, int posicionVerificador)
        {
            int total = 0;
            int verificador = int.Parse(ruc.Substring(posicionVerificador, 1));

            for (int i = 0; i < coeficientes.Length; i++)
            {
                total += int.Parse(ruc.Substring(i, 1)) * coeficientes[i];
            }

            int residuo = total % 11;
            int resultado = (residuo == 0) ? 0 : 11 - residuo;

            return resultado == verificador;
        }
    }
}