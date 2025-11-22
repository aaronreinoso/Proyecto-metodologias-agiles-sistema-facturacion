using SistemaFacturacionSRI.BlazorApp.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SistemaFacturacionSRI.BlazorApp.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- INICIO DE CONFIGURACIÓN DE HTTPCLIENT ---
// Registra el HttpClient como un servicio
builder.Services.AddScoped(sp => new HttpClient
{
    // ¡IMPORTANTE! Usa la URL de tu API que anotaste en el Paso 1
    BaseAddress = new Uri("https://localhost:7139")//;http://localhost:5183 ")
});
// --- FIN DE CONFIGURACIÓN DE HTTPCLIENT ---

// --- SEGURIDAD ---
builder.Services.AddAuthorizationCore(); // Habilita el sistema de permisos
builder.Services.AddScoped<SimpleAuthStateProvider>(); // Tu servicio personalizado
// Conecta tu servicio con el sistema estándar de Blazor
builder.Services.AddScoped<AuthenticationStateProvider>(p => p.GetRequiredService<SimpleAuthStateProvider>());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
