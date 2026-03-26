using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turnify.Api.Migrations
{
    /// <inheritdoc />
    public partial class Sincronizacion_Final_Fase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_horarios_atencion_proveedor_id",
                table: "horarios_atencion",
                column: "proveedor_id");

            migrationBuilder.AddForeignKey(
                name: "FK_horarios_atencion_proveedores_proveedor_id",
                table: "horarios_atencion",
                column: "proveedor_id",
                principalTable: "proveedores",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_horarios_atencion_proveedores_proveedor_id",
                table: "horarios_atencion");

            migrationBuilder.DropIndex(
                name: "IX_horarios_atencion_proveedor_id",
                table: "horarios_atencion");
        }
    }
}
