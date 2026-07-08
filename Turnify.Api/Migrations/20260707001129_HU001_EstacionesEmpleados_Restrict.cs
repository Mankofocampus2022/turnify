using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turnify.Api.Migrations
{
    /// <inheritdoc />
    public partial class HU001_EstacionesEmpleados_Restrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "empleado_id",
                table: "citas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estacion_id",
                table: "citas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "empleados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    tipo_contrato = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    valor_contrato = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleados", x => x.id);
                    table.ForeignKey(
                        name: "FK_empleados_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_empleados_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "estaciones_trabajo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estaciones_trabajo", x => x.id);
                    table.ForeignKey(
                        name: "FK_estaciones_trabajo_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "nombre" },
                values: new object[] { new Guid("99a2b3c4-e5f6-4789-90ab-c1d2e3f40099"), "Staff" });

            migrationBuilder.CreateIndex(
                name: "IX_citas_empleado_id",
                table: "citas",
                column: "empleado_id");

            migrationBuilder.CreateIndex(
                name: "IX_citas_estacion_id",
                table: "citas",
                column: "estacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_proveedor_id",
                table: "empleados",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_usuario_id",
                table: "empleados",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_estaciones_trabajo_proveedor_id",
                table: "estaciones_trabajo",
                column: "proveedor_id");

            migrationBuilder.AddForeignKey(
                name: "FK_citas_empleados_empleado_id",
                table: "citas",
                column: "empleado_id",
                principalTable: "empleados",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_citas_estaciones_trabajo_estacion_id",
                table: "citas",
                column: "estacion_id",
                principalTable: "estaciones_trabajo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_citas_empleados_empleado_id",
                table: "citas");

            migrationBuilder.DropForeignKey(
                name: "FK_citas_estaciones_trabajo_estacion_id",
                table: "citas");

            migrationBuilder.DropTable(
                name: "empleados");

            migrationBuilder.DropTable(
                name: "estaciones_trabajo");

            migrationBuilder.DropIndex(
                name: "IX_citas_empleado_id",
                table: "citas");

            migrationBuilder.DropIndex(
                name: "IX_citas_estacion_id",
                table: "citas");

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("99a2b3c4-e5f6-4789-90ab-c1d2e3f40099"));

            migrationBuilder.DropColumn(
                name: "empleado_id",
                table: "citas");

            migrationBuilder.DropColumn(
                name: "estacion_id",
                table: "citas");
        }
    }
}
