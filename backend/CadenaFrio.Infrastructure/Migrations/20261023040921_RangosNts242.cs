using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RangosNts242 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vacunas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Fuente", "LimiteMinimo", "OptimoMinimo" },
                values: new object[] { "NTS N° 242-MINSA/DGIESP-2026", 2.0, 3.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Vacunas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Fuente", "LimiteMinimo", "OptimoMinimo" },
                values: new object[] { "Directiva Sanitaria N° 133-MINSA-2021", 0.0, 2.0 });
        }
    }
}
