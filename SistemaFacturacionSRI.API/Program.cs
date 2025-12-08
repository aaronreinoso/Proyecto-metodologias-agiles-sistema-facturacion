using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.Services;
using SistemaFacturacionSRI.Infrastructure.Services.Pdf;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using System.Text;
using System.Text.Json.Serialization;
using SistemaFacturacionSRI.API.Workers;

var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// --- INICIO DE CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:5202")
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});
// --- FIN DE CONFIGURACIÓN DE CORS ---

// --- INICIO DE CONFIGURACIÓN DE EF CORE ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FacturaElectronicaDB"),
        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    ));
// --- FIN DE CONFIGURACIÓN DE EF CORE ---

// --- REGISTRO DE SERVICIOS PERSONALIZADOS
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ILoteProductoService, LoteProductoService>();
builder.Services.AddScoped<IFacturaService, FacturaService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<FacturaElectronicaService>(); // Para lógica de XML y firma

// --- SERVICIOS DE FACTURACIÓN Y SRI ---
builder.Services.AddScoped<ClaveAccesoService>();
builder.Services.AddScoped<SriSoapClient>();

builder.Services.AddHostedService<SriRetryWorker>();


// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// --- CONFIGURACIÓN DE AUTENTICACIÓN JWT (Paso 4) ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});
// ----------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigins);

// *** ORDEN CORREGIDO ***
// 1. Authentication debe ir ANTES de Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();