using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacionSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CamposEstadoSRIFechaAutorizacionMensajeErrorSRI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoSRI",
                table: "Facturas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAutorizacion",
                table: "Facturas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensajeErrorSRI",
                table: "Facturas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoSRI",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaAutorizacion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "MensajeErrorSRI",
                table: "Facturas");
        }
    }
}
