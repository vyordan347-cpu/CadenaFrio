using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DosNiveles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OptimoMaximo",
                table: "RangosTemperatura",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OptimoMinimo",
                table: "RangosTemperatura",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRuptura",
                table: "Alertas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "Alertas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "OptimoMaximo",
                table: "Alertas",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OptimoMinimo",
                table: "Alertas",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
            // Datos existentes: los rangos anteriores tenían un solo nivel,
// así que su rango óptimo es igual a su límite (se comportan igual que antes).
migrationBuilder.Sql(
    "UPDATE \"RangosTemperatura\" SET \"OptimoMinimo\" = \"TemperaturaMinima\", \"OptimoMaximo\" = \"TemperaturaMaxima\";");

// Las alertas anteriores se abrieron al salir del único rango: fueron rupturas desde el inicio.
migrationBuilder.Sql(
    "UPDATE \"Alertas\" SET \"OptimoMinimo\" = \"RangoMinimo\", \"OptimoMaximo\" = \"RangoMaximo\", \"Nivel\" = 2, \"FechaRuptura\" = \"Inicio\";");    
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptimoMaximo",
                table: "RangosTemperatura");

            migrationBuilder.DropColumn(
                name: "OptimoMinimo",
                table: "RangosTemperatura");

            migrationBuilder.DropColumn(
                name: "FechaRuptura",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "OptimoMaximo",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "OptimoMinimo",
                table: "Alertas");
        }
    }
}
