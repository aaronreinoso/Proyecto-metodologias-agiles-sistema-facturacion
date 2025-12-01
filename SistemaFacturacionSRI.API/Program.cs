using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Infrastructure.Persistence; // La ubicaci�n de tu AppDbContext
using SistemaFacturacionSRI.Infrastructure.Services; // para ProductoService y ClienteService
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// --- INICIO DE CONFIGURACI�N DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // 2. �IMPORTANTE! Usa la URL de tu BlazorApp que anotaste en el Paso 1
                          policy.WithOrigins("http://localhost:5202")
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});
// --- FIN DE CONFIGURACI�N DE CORS ---

// --- INICIO DE CONFIGURACI�N DE EF CORE ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FacturaElectronicaDB"),
        // Indica a EF Core d�nde buscar las migraciones (en el proyecto Infrastructure)
        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    ));
// --- FIN DE CONFIGURACI�N DE EF CORE ---

// --- REGISTRO DE SERVICIOS PERSONALIZADOS
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ILoteProductoService, LoteProductoService>();
builder.Services.AddScoped<IFacturaService, FacturaService>();

// --- NUEVOS SERVICIOS DE FACTURACI�N Y SRI ---
builder.Services.AddScoped<ClaveAccesoService>();
builder.Services.AddScoped<SriSoapClient>();

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(x =>
{
    // Esta opci�n evita el bucle infinito ignorando el objeto repetido
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

builder.Services.AddScoped<FacturaElectronicaService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthorization();

app.MapControllers();

app.Run();
