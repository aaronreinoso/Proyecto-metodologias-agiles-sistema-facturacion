using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFacturacionSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjustesMiosFacturacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NombreArchivo",
                table: "ConfiguracionesSRI",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "FirmaElectronica",
                table: "ConfiguracionesSRI",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaSubida",
                table: "ConfiguracionesSRI",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "ClaveFirma",
                table: "ConfiguracionesSRI",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "CodigoEstablecimiento",
                table: "ConfiguracionesSRI",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodigoPuntoEmision",
                table: "ConfiguracionesSRI",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContribuyenteEspecial",
                table: "ConfiguracionesSRI",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionEstablecimiento",
                table: "ConfiguracionesSRI",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionMatriz",
                table: "ConfiguracionesSRI",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NombreComercial",
                table: "ConfiguracionesSRI",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObligadoContabilidad",
                table: "ConfiguracionesSRI",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "ConfiguracionesSRI",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ruc",
                table: "ConfiguracionesSRI",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoEstablecimiento",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "CodigoPuntoEmision",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "ContribuyenteEspecial",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "DireccionEstablecimiento",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "DireccionMatriz",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "NombreComercial",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "ObligadoContabilidad",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "ConfiguracionesSRI");

            migrationBuilder.DropColumn(
                name: "Ruc",
                table: "ConfiguracionesSRI");

            migrationBuilder.AlterColumn<string>(
                name: "NombreArchivo",
                table: "ConfiguracionesSRI",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "FirmaElectronica",
                table: "ConfiguracionesSRI",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaSubida",
                table: "ConfiguracionesSRI",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaveFirma",
                table: "ConfiguracionesSRI",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
