using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionAnexo13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CausaRuptura",
                table: "Alertas",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalleCausa",
                table: "Alertas",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalleRestablecimiento",
                table: "Alertas",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaRestablecimiento",
                table: "Alertas",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VacunasInmovilizadas",
                table: "Alertas",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CausaRuptura",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "DetalleCausa",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "DetalleRestablecimiento",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "FormaRestablecimiento",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "VacunasInmovilizadas",
                table: "Alertas");
        }
    }
}
