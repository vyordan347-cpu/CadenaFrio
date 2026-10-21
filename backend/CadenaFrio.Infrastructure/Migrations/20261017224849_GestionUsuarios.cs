using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GestionUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarContrasena",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SelloSeguridad",
                table: "Usuarios",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
                // Cada usuario existente recibe un sello aleatorio propio.
    migrationBuilder.Sql(
        "UPDATE \"Usuarios\" SET \"SelloSeguridad\" = lower(hex(randomblob(16)));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebeCambiarContrasena",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SelloSeguridad",
                table: "Usuarios");
        }
    }
}
