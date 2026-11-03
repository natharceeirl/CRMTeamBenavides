using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCodigoActivacionCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoActivacionHash",
                table: "Clientes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodigoActivacionIntentos",
                table: "Clientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CodigoActivacionVence",
                table: "Clientes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoActivacionHash",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CodigoActivacionIntentos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CodigoActivacionVence",
                table: "Clientes");
        }
    }
}
