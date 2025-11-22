using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.BlazorApp.Auth
{
    public class SimpleAuthStateProvider : AuthenticationStateProvider
    {
        // Usuario anónimo por defecto
        private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(_currentUser));
        }

        // Llamaremos a esto cuando login  exitoso
        public void IniciarSesion(Usuario usuario)
        {
            var claims = new List<Claim>
    {
        // Guardamos el ID en el claim NameIdentifier (estándar)
        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new Claim(ClaimTypes.Name, usuario.NombreUsuario),
        new Claim(ClaimTypes.Role, usuario.Rol)
    };

            var identity = new ClaimsIdentity(claims, "apiauth");
            _currentUser = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        // Llamaremos a esto para salir
        public void CerrarSesion()
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}