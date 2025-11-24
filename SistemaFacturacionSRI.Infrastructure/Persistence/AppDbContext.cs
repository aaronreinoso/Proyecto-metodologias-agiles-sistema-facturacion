using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        // --- Definición de Tablas (DbSets) ---
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<LoteProducto> LotesProducto { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<DetalleFactura> DetallesFactura { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<ConfiguracionSRI> ConfiguracionesSRI { get; set; }

        // NUEVO: Tabla de Tarifas de IVA
        public DbSet<TarifaIva> TarifasIva { get; set; }

     

        public DbSet<AjusteInventario> AjustesInventario { get; set; }



        // --- Constructor necesario para la Inyección de Dependencias ---
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // --- Configuración de modelo ---
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AjusteInventario>().ToTable("AjustesInventario");


            // Configuraciones EXISTENTES de Producto
            // NOTA: Estas propiedades de Producto se eliminarán en el siguiente paso, 
            // pero se mantienen por ahora para que EF Core pueda generar la migración
            // de forma incremental (Add / Drop Column).
            modelBuilder.Entity<Producto>(entity =>
            {
                entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
   
                entity.Property(e => e.PorcentajeICE).HasColumnType("decimal(5,2)");
                entity.Property(e => e.PorcentajeIRBPNR).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Stock).HasColumnType("decimal(18,2)");

                entity.Property(e => e.CodigoPrincipal).IsRequired().HasMaxLength(25);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(300);
            });

            // --- Datos iniciales de Usuarios ---
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario { Id = 1, NombreUsuario = "admin", Password = "12345", Rol = "Administrador", Estado = true },
                new Usuario { Id = 2, NombreUsuario = "vendedor", Password = "12345", Rol = "Empleado", Estado = true }
            );

            // --- NUEVO: Datos iniciales para Tarifas de IVA ---
            modelBuilder.Entity<TarifaIva>().HasData(
                new TarifaIva { Id = 1, CodigoSRI = "0", Porcentaje = 0.00m },   // 0%
                new TarifaIva { Id = 2, CodigoSRI = "2", Porcentaje = 12.00m },  // 12%
                new TarifaIva { Id = 3, CodigoSRI = "4", Porcentaje = 15.00m },  // 15%
                new TarifaIva { Id = 4, CodigoSRI = "5", Porcentaje = 5.00m },   // 5% (Construcción)
                new TarifaIva { Id = 5, CodigoSRI = "8", Porcentaje = 8.00m }    // 8%
            );



        }
    }
}