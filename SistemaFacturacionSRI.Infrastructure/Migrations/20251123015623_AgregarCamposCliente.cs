using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacionSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pais",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoIdentificacion",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pais",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TipoIdentificacion",
                table: "Clientes");
        }
    }
}
