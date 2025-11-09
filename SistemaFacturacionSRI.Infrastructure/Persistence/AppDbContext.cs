using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.Entities; // Importa tus entidades

namespace SistemaFacturacionSRI.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        // --- Definición de Tablas (DbSets) ---
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }

        // --- Constructor necesario para la Inyección de Dependencias ---
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // --- Configuración de modelo ---
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración precisa para la entidad Producto
            modelBuilder.Entity<Producto>(entity =>
            {
                // Definir tipos y precisión de columnas decimales (evita truncamientos)
                entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PrecioCompra).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PorcentajeIVA).HasColumnType("decimal(5,2)");
                entity.Property(e => e.PorcentajeICE).HasColumnType("decimal(5,2)");
                entity.Property(e => e.PorcentajeIRBPNR).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Stock).HasColumnType("decimal(18,2)");

                // Definir restricciones adicionales opcionales
                entity.Property(e => e.CodigoPrincipal)
                      .IsRequired()
                      .HasMaxLength(25);

                entity.Property(e => e.Descripcion)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(e => e.TipoProducto)
                      .IsRequired()
                      .HasMaxLength(20);
            });

            // Puedes agregar otras configuraciones de entidades aquí en el futuro.
        }
    }
}
