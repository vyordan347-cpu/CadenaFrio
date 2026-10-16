using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sincronizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lecturas_EquipoId_FechaMedicion",
                table: "Lecturas");

            migrationBuilder.AddColumn<bool>(
                name: "DetectadaTarde",
                table: "Alertas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Lecturas_EquipoId_FechaMedicion",
                table: "Lecturas",
                columns: new[] { "EquipoId", "FechaMedicion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lecturas_EquipoId_FechaMedicion",
                table: "Lecturas");

            migrationBuilder.DropColumn(
                name: "DetectadaTarde",
                table: "Alertas");

            migrationBuilder.CreateIndex(
                name: "IX_Lecturas_EquipoId_FechaMedicion",
                table: "Lecturas",
                columns: new[] { "EquipoId", "FechaMedicion" });
        }
    }
}
