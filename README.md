# Sistema de Facturación Electrónica SRI

Este proyecto implementa un **Sistema de Facturación Electrónica** desarrollado siguiendo metodologías ágiles, orientado al cumplimiento de los requisitos establecidos por el SRI (Servicio de Rentas Internas) de Ecuador. Está enfocado en la gestión de productos, clientes y comprobantes electrónicos, y está construido con **Blazor** y una arquitectura de capas moderna .NET.

## Características principales

- **Gestión de Clientes:** Permite crear, editar y listar clientes desde una interfaz intuitiva.
- **Gestión de Productos:** Soporta productos y servicios, registro de sus datos requeridos por el SRI (como código, descripción, IVA, ICE, IRBPNR, precios, stock, etc).
- **API RESTful:** Controladores para productos y clientes, con validación robusta, manejo de errores y respuestas amigables para integraciones o uso desde el frontend Blazor.
- **Persistencia de Datos:** Utiliza Entity Framework Core, con migraciones explícitas y separación clara de las entidades de dominio ("Producto", "Cliente").
- **Arquitectura limpia:** Separación por capas (API, BlazorApp, Domain, Infrastructure), facilitando el mantenimiento, escalabilidad y pruebas.

## Estructura del Proyecto

- **SistemaFacturacionSRI.BlazorApp**:  
  Aplicación web con Blazor, para la interacción de usuarios, manejo visual y flujo de trabajo de facturación.
  - `Components/Pages/Clientes.razor`: Página de gestión de clientes.
  - `Components/Pages/Productos.razor`: Página de gestión de productos.
  - `Components/Layout/MainLayout.razor`: Layout principal.
  - `Components/Layout/NavMenu.razor`: Menú de navegación.

- **SistemaFacturacionSRI.API**:  
  API que expone los endpoints de clientes y productos (`ProductosController`, ...).

- **SistemaFacturacionSRI.Domain**:  
  Entidades y lógicas principales (ej. `Producto.cs`, con los campos necesarios según SRI).

- **SistemaFacturacionSRI.Infrastructure**:  
  Persistencia y configuración de la base de datos (`AppDbContext`, migraciones).

## Ejemplo de Entidad Product
```csharp
public class Producto {
    public int Id { get; set; }
    public string CodigoPrincipal { get; set; }
    public string CodigoAuxiliar { get; set; }
    public string Descripcion { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeIVA { get; set; }
    public string TipoProducto { get; set; }
    public decimal? PrecioCompra { get; set; }
    public decimal? Stock { get; set; }
    // Campos fiscales: ICE, IRBPNR, etc.
    public bool Estado { get; set; }
    public DateTime FechaRegistro { get; set; }
}
```

## Tecnologías Utilizadas

- [.NET 8 / Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- Entity Framework Core
- Arquitectura de capas (Domain, Infrastructure, API, UI)
- Métodos y validaciones alineadas a normativas SRI

## Uso

1. Clona el proyecto.
2. Configura la cadena de conexión en `appsettings.json`.
3. Ejecuta las migraciones de EF Core.
4. Levanta la API y la BlazorApp.
5. Navega por las páginas de "Clientes" y "Productos" para administrar datos de facturación.

## Metodología Ágil

El desarrollo fue realizado aplicando prácticas ágiles:
- Entregas iterativas.
- Retroalimentación continua.
- Priorización de valor para el usuario final.
- Documentación clara y separación por módulos.

## Licencia

Este software se distribuye bajo la licencia MIT. Consulta el archivo LICENSE para más información.

---

*Desarrollado por [aaronreinoso](https://github.com/aaronreinoso) y colaboradores.*
