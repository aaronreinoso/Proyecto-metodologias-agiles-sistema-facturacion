using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Infrastructure.Persistence; // La ubicación de tu AppDbContext
using SistemaFacturacionSRI.Infrastructure.Services; // para ProductoService y ClienteService


// ...
var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// --- INICIO DE CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          // 2. ¡IMPORTANTE! Usa la URL de tu BlazorApp que anotaste en el Paso 1
                          policy.WithOrigins("http://localhost:5202")
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});
// --- FIN DE CONFIGURACIÓN DE CORS ---

// --- INICIO DE CONFIGURACIÓN DE EF CORE ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FacturaElectronicaDB"),
        // Indica a EF Core dónde buscar las migraciones (en el proyecto Infrastructure)
        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    ));
// --- FIN DE CONFIGURACIÓN DE EF CORE ---

// --- REGISTRO DE SERVICIOS PERSONALIZADOS
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ILoteProductoService, LoteProductoService>();
builder.Services.AddScoped<IFacturaService, FacturaService>();


// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

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
