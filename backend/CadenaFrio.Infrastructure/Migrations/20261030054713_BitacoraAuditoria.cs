using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BitacoraAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bitacora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Categoria = table.Column<int>(type: "INTEGER", nullable: false),
                    Accion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Fecha",
                table: "Bitacora",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bitacora");
        }
    }
}
