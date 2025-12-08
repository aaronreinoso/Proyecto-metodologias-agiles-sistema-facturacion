using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace SistemaFacturacionSRI.BlazorApp.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;

        public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
        {
            _localStorage = localStorage;
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            string token = null;

            try
            {
                // Intentamos leer el token. Si estamos pre-renderizando (Server), esto fallará.
                token = await _localStorage.GetItemAsStringAsync("authToken");
            }
            catch (InvalidOperationException)
            {
                // ERROR CONTROLADO: Estamos en el servidor (pre-render).
                // Asumimos que no hay usuario todavía y dejamos pasar.
            }

            var identity = new ClaimsIdentity();
            _http.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    // Si hay token, configuramos el cliente HTTP y extraemos claims
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));
                    identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
                }
                catch
                {
                    // Si el token está corrupto o mal formado, lo ignoramos
                    _http.DefaultRequestHeaders.Authorization = null;
                    identity = new ClaimsIdentity();
                }
            }

            var user = new ClaimsPrincipal(identity);
            var state = new AuthenticationState(user);

            NotifyAuthenticationStateChanged(Task.FromResult(state));

            return state;
        }
        // Método auxiliar para leer los datos encriptados del token
        public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            var claims = new List<Claim>();

            foreach (var kvp in keyValuePairs)
            {
                // Mapeo manual de claves cortas a Tipos de Claims de .NET
                var key = kvp.Key;
                var value = kvp.Value.ToString();

                if (key == "nameid") key = ClaimTypes.NameIdentifier;
                if (key == "unique_name") key = ClaimTypes.Name;
                if (key == "role") key = ClaimTypes.Role;

                claims.Add(new Claim(key, value));
            }

            return claims;
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }


        // ... (código anterior de GetAuthenticationStateAsync y ParseClaims ...)

        public async Task Logout()
        {
            // 1. Borrar el token del navegador
            await _localStorage.RemoveItemAsync("authToken");

            // 2. Limpiar la cabecera HTTP
            _http.DefaultRequestHeaders.Authorization = null;

            // 3. Notificar a Blazor que ya no hay usuario
            var nobody = new ClaimsPrincipal(new ClaimsIdentity());
            var state = new AuthenticationState(nobody);

            NotifyAuthenticationStateChanged(Task.FromResult(state));
        }


    }




}
