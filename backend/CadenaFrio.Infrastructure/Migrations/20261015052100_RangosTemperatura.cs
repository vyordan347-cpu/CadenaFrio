using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RangosTemperatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RangosTemperatura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipoId = table.Column<int>(type: "INTEGER", nullable: false),
                    TemperaturaMinima = table.Column<double>(type: "REAL", nullable: false),
                    TemperaturaMaxima = table.Column<double>(type: "REAL", nullable: false),
                    FechaCambio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModificadoPor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RangosTemperatura", x => x.Id);
                    table.CheckConstraint("CK_RangoTemperatura_MinimaMenorQueMaxima", "\"TemperaturaMinima\" < \"TemperaturaMaxima\"");
                    table.ForeignKey(
                        name: "FK_RangosTemperatura_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RangosTemperatura_EquipoId_FechaCambio",
                table: "RangosTemperatura",
                columns: new[] { "EquipoId", "FechaCambio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RangosTemperatura");
        }
    }
}
