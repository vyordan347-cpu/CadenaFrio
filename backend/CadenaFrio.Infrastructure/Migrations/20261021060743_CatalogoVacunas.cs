using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoVacunas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detalle",
                table: "RangosTemperatura",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vacunas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OptimoMinimo = table.Column<double>(type: "REAL", nullable: false),
                    OptimoMaximo = table.Column<double>(type: "REAL", nullable: false),
                    LimiteMinimo = table.Column<double>(type: "REAL", nullable: false),
                    LimiteMaximo = table.Column<double>(type: "REAL", nullable: false),
                    Fuente = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacunas", x => x.Id);
                    table.CheckConstraint("CK_Vacuna_RangosCoherentes", "\"LimiteMinimo\" <= \"OptimoMinimo\" AND \"OptimoMinimo\" < \"OptimoMaximo\" AND \"OptimoMaximo\" <= \"LimiteMaximo\"");
                });

            migrationBuilder.CreateTable(
                name: "EquipoVacunas",
                columns: table => new
                {
                    EquipoId = table.Column<int>(type: "INTEGER", nullable: false),
                    VacunaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipoVacunas", x => new { x.EquipoId, x.VacunaId });
                    table.ForeignKey(
                        name: "FK_EquipoVacunas_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipoVacunas_Vacunas_VacunaId",
                        column: x => x.VacunaId,
                        principalTable: "Vacunas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Vacunas",
                columns: new[] { "Id", "Activo", "Fuente", "LimiteMaximo", "LimiteMinimo", "Nombre", "OptimoMaximo", "OptimoMinimo" },
                values: new object[] { 1, true, "Directiva Sanitaria N° 133-MINSA-2021", 8.0, 0.0, "Esquema regular (PNI)", 7.0, 2.0 });

            migrationBuilder.CreateIndex(
                name: "IX_EquipoVacunas_VacunaId",
                table: "EquipoVacunas",
                column: "VacunaId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacunas_Nombre",
                table: "Vacunas",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipoVacunas");

            migrationBuilder.DropTable(
                name: "Vacunas");

            migrationBuilder.DropColumn(
                name: "Detalle",
                table: "RangosTemperatura");
        }
    }
}
