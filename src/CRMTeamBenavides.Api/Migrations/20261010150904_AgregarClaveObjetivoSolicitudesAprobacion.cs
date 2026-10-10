using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarClaveObjetivoSolicitudesAprobacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveObjetivo",
                table: "SolicitudesAprobacion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DetalleId",
                table: "SolicitudesAprobacion",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_DetalleId",
                table: "SolicitudesAprobacion",
                column: "DetalleId");

            // Las solicitudes existentes llevaban el ítem dentro del texto como
            // [Detalle:<id>]: pasa a su columna y sale del texto que ve Gerencia.
            migrationBuilder.Sql("""
                UPDATE "SolicitudesAprobacion"
                SET "DetalleId" = (substring("DetalleCambio" from '\[Detalle:\s*([0-9A-Fa-f-]{36})\s*\]'))::uuid,
                    "ClaveObjetivo" = 'detalle_' || lower(substring("DetalleCambio" from '\[Detalle:\s*([0-9A-Fa-f-]{36})\s*\]')),
                    "DetalleCambio" = btrim(regexp_replace("DetalleCambio", '\s*\[Detalle:[^\]]*\]', '', 'gi'))
                WHERE "DetalleId" IS NULL
                  AND "DetalleCambio" ~* '\[Detalle:\s*[0-9a-f-]{36}\s*\]';
                """);

            // [Objetivo:x] tenía prioridad sobre cualquier otra clave: se conserva.
            migrationBuilder.Sql("""
                UPDATE "SolicitudesAprobacion"
                SET "ClaveObjetivo" = lower(btrim(substring("DetalleCambio" from '\[Objetivo:([^\]]+)\]')))
                WHERE "DetalleCambio" ~* '\[Objetivo:[^\]]+\]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El texto limpio no vuelve a llevar la marca [Detalle:<id>]: esas
            // solicitudes antiguas quedan sin ítem si se revierte la migración.
            migrationBuilder.DropIndex(
                name: "IX_SolicitudesAprobacion_DetalleId",
                table: "SolicitudesAprobacion");

            migrationBuilder.DropColumn(
                name: "ClaveObjetivo",
                table: "SolicitudesAprobacion");

            migrationBuilder.DropColumn(
                name: "DetalleId",
                table: "SolicitudesAprobacion");
        }
    }
}
