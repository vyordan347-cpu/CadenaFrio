using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CadenaFrio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AvisosTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoVinculacionTelegram",
                table: "Usuarios",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Correo",
                table: "Usuarios",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsContactoEscalamiento",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecibeAlertas",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Usuarios",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEscalamiento",
                table: "Alertas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaReconocimiento",
                table: "Alertas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconocidaPor",
                table: "Alertas",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoVinculacionTelegram",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Correo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EsContactoEscalamiento",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "RecibeAlertas",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaEscalamiento",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "FechaReconocimiento",
                table: "Alertas");

            migrationBuilder.DropColumn(
                name: "ReconocidaPor",
                table: "Alertas");
        }
    }
}
