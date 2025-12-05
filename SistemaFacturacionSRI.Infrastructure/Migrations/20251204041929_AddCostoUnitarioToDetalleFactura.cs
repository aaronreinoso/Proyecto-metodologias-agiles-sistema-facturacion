using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacionSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCostoUnitarioToDetalleFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "DetallesFactura",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "DetallesFactura");
        }
    }
}
