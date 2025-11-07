using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.Entities; // Importa tus entidades

namespace SistemaFacturacionSRI.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        // Define las tablas que EF Core debe administrar
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }



        // Constructor necesario para la Inyección de Dependencias
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuramos la precisión para la propiedad PrecioUnitario de la entidad Producto
            modelBuilder.Entity<Producto>(entity =>
            {
                entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PrecioCompra).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PorcentajeIVA).HasColumnType("decimal(5,2)");
            });



        }
    }
}