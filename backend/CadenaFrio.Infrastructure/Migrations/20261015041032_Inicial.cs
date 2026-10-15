using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Ubicacion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ClaveDispositivo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lecturas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Temperatura = table.Column<double>(type: "REAL", nullable: false),
                    FechaMedicion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lecturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lecturas_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Equipos",
                columns: new[] { "Id", "Activo", "ClaveDispositivo", "FechaRegistro", "Nombre", "Tipo", "Ubicacion" },
                values: new object[] { 1, true, "clave-prueba-001", new DateTime(2026, 10, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Refrigeradora de prueba", 1, "Entorno de desarrollo" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_ClaveDispositivo",
                table: "Equipos",
                column: "ClaveDispositivo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecturas_EquipoId_FechaMedicion",
                table: "Lecturas",
                columns: new[] { "EquipoId", "FechaMedicion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lecturas");

            migrationBuilder.DropTable(
                name: "Equipos");
        }
    }
}
