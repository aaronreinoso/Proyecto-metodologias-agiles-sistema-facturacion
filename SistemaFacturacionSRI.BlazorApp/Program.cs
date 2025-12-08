using SistemaFacturacionSRI.BlazorApp.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SistemaFacturacionSRI.BlazorApp.Auth;
using Blazored.LocalStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// =============================
// 1. LOCAL STORAGE
// =============================
builder.Services.AddBlazoredLocalStorage();

// =============================
// 2. HTTP CLIENT
// =============================
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7139")
});

// =============================
// 3. AUTORIZACIÓN Y AUTENTICACIÓN
// =============================
builder.Services.AddAuthorizationCore();

// Tu proveedor de autenticación personalizado
builder.Services.AddScoped<CustomAuthStateProvider>();

// Registrar como servicio estándar de Blazor
builder.Services.AddScoped<AuthenticationStateProvider>(p =>
    p.GetRequiredService<CustomAuthStateProvider>());

// =============================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
