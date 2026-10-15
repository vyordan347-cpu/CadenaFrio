using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DetectorSilencio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RevisionRequeridaDesde",
                table: "Alertas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecturas_EquipoId_FechaRecepcion",
                table: "Lecturas",
                columns: new[] { "EquipoId", "FechaRecepcion" });
            // Las rupturas existentes exigen revisión desde que llegaron a ruptura.
migrationBuilder.Sql(
    "UPDATE \"Alertas\" SET \"RevisionRequeridaDesde\" = \"FechaRuptura\" WHERE \"Nivel\" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lecturas_EquipoId_FechaRecepcion",
                table: "Lecturas");

            migrationBuilder.DropColumn(
                name: "RevisionRequeridaDesde",
                table: "Alertas");
        }
    }
}
