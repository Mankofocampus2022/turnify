using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turnify.Api.Migrations
{
    /// <inheritdoc />
    public partial class CambiarA_DateTimeOffset_Mundial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 🛡️ ELIMINACIÓN DE LLAVES Y LLAVES FORÁNEAS (CON CONTROL DE EXISTENCIA) ---
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_citas_clientes_cliente_id')
                    ALTER TABLE [citas] DROP CONSTRAINT [FK_citas_clientes_cliente_id];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_clientes_usuarios_usuario_id')
                    ALTER TABLE [clientes] DROP CONSTRAINT [FK_clientes_usuarios_usuario_id];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_servicios_proveedores_ProveedorId')
                    ALTER TABLE [servicios] DROP CONSTRAINT [FK_servicios_proveedores_ProveedorId];
            ");

            // --- 🛡️ ELIMINACIÓN DE ÍNDICES VLEJOS (CON CONTROL DE EXISTENCIA) ---
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_proveedores_usuario_id' AND object_id = OBJECT_ID('[proveedores]'))
                    DROP INDEX [IX_proveedores_usuario_id] ON [proveedores];
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_clientes_usuario_id' AND object_id = OBJECT_ID('[clientes]'))
                    DROP INDEX [IX_clientes_usuario_id] ON [clientes];
            ");

            // --- 🛡️ ACTUALIZACIÓN DE COLUMNAS PARA USUARIOS ---
            migrationBuilder.AlterColumn<bool>(
                name: "activo",
                table: "usuarios",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[usuarios]') AND name = 'ResetToken')
                    ALTER TABLE [usuarios] ADD [ResetToken] nvarchar(max) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[usuarios]') AND name = 'ResetTokenExpires')
                    ALTER TABLE [usuarios] ADD [ResetTokenExpires] datetime2 NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[usuarios]') AND name = 'esta_bloqueado')
                    ALTER TABLE [usuarios] ADD [esta_bloqueado] bit NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[usuarios]') AND name = 'suscripcion_fin')
                    ALTER TABLE [usuarios] ADD [suscripcion_fin] datetime2 NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[usuarios]') AND name = 'ultima_conexion')
                    ALTER TABLE [usuarios] ADD [ultima_conexion] datetime2 NULL;
            ");

            // --- 🛡️ ACTUALIZACIÓN DE COLUMNAS PARA SERVICIOS ---
            migrationBuilder.AlterColumn<Guid>(
                name: "ProveedorId",
                table: "servicios",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "servicios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Activo",
                table: "servicios",
                type: "int",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[servicios]') AND name = 'Categoria')
                    ALTER TABLE [servicios] ADD [Categoria] nvarchar(50) NOT NULL DEFAULT '';
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[servicios]') AND name = 'ComisionPorcentaje')
                    ALTER TABLE [servicios] ADD [ComisionPorcentaje] decimal(5,2) NOT NULL DEFAULT 0;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[servicios]') AND name = 'ImagenUrl')
                    ALTER TABLE [servicios] ADD [ImagenUrl] nvarchar(max) NULL;
            ");

            // --- 🛡️ ACTUALIZACIÓN DE COLUMNAS PARA PROVEEDORES ---
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[proveedores]') AND name = 'categoria')
                    ALTER TABLE [proveedores] ADD [categoria] nvarchar(max) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[proveedores]') AND name = 'email')
                    ALTER TABLE [proveedores] ADD [email] nvarchar(150) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[proveedores]') AND name = 'telefono')
                    ALTER TABLE [proveedores] ADD [telefono] nvarchar(20) NULL;
            ");

            // --- 🛡️ ACTUALIZACIÓN DE COLUMNAS PARA CLIENTES ---
            migrationBuilder.AlterColumn<Guid>(
                name: "usuario_id",
                table: "clientes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "clientes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            // --- 🛡️ ACTUALIZACIÓN DE COLUMNAS PARA CITAS (LOGICA MUNDIAL) ---
            migrationBuilder.AlterColumn<string>(
                name: "modalidad",
                table: "citas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // 🚀 IMPACTO DE FECHAS GLOBALES DATETIMEOFFSET
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "fecha_creacion",
                table: "citas",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "fecha",
                table: "citas",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "citas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'codigo_verificacion')
                    ALTER TABLE [citas] ADD [codigo_verificacion] nvarchar(10) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'costo_domicilio')
                    ALTER TABLE [citas] ADD [costo_domicilio] decimal(18,2) NOT NULL DEFAULT 0;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'latitud')
                    ALTER TABLE [citas] ADD [latitud] decimal(18,10) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'longitud')
                    ALTER TABLE [citas] ADD [longitud] decimal(18,10) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'metodo_registro')
                    ALTER TABLE [citas] ADD [metodo_registro] nvarchar(max) NULL;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[citas]') AND name = 'row_version')
                    ALTER TABLE [citas] ADD [row_version] timestamp NOT NULL;
            ");

            // --- 🛡️ RECONSTRUCCIÓN DE ÍNDICES Y LLAVES FORÁNEAS DEFENSIVAS ---
            migrationBuilder.CreateIndex(
                name: "IX_proveedores_usuario_id",
                table: "proveedores",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_usuario_id",
                table: "clientes",
                column: "usuario_id",
                unique: true,
                filter: "[usuario_id] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_citas_clientes_cliente_id",
                table: "citas",
                column: "cliente_id",
                principalTable: "clientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_clientes_usuarios_usuario_id",
                table: "clientes",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_servicios_proveedores_ProveedorId",
                table: "servicios",
                column: "ProveedorId",
                principalTable: "proveedores",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mantenemos intacto tu rollback original por consistencia estructural
            migrationBuilder.DropForeignKey(
                name: "FK_citas_clientes_cliente_id",
                table: "citas");

            migrationBuilder.DropForeignKey(
                name: "FK_clientes_usuarios_usuario_id",
                table: "clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_servicios_proveedores_ProveedorId",
                table: "servicios");

            migrationBuilder.DropIndex(
                name: "IX_proveedores_usuario_id",
                table: "proveedores");

            migrationBuilder.DropIndex(
                name: "IX_clientes_usuario_id",
                table: "clientes");

            migrationBuilder.DropColumn(name: "ResetToken", table: "usuarios");
            migrationBuilder.DropColumn(name: "ResetTokenExpires", table: "usuarios");
            migrationBuilder.DropColumn(name: "esta_bloqueado", table: "usuarios");
            migrationBuilder.DropColumn(name: "suscripcion_fin", table: "usuarios");
            migrationBuilder.DropColumn(name: "ultima_conexion", table: "usuarios");
            migrationBuilder.DropColumn(name: "Categoria", table: "servicios");
            migrationBuilder.DropColumn(name: "ComisionPorcentaje", table: "servicios");
            migrationBuilder.DropColumn(name: "ImagenUrl", table: "servicios");
            migrationBuilder.DropColumn(name: "categoria", table: "proveedores");
            migrationBuilder.DropColumn(name: "email", table: "proveedores");
            migrationBuilder.DropColumn(name: "telefono", table: "proveedores");
            migrationBuilder.DropColumn(name: "codigo_verificacion", table: "citas");
            migrationBuilder.DropColumn(name: "costo_domicilio", table: "citas");
            migrationBuilder.DropColumn(name: "latitud", table: "citas");
            migrationBuilder.DropColumn(name: "longitud", table: "citas");
            migrationBuilder.DropColumn(name: "metodo_registro", table: "citas");
            migrationBuilder.DropColumn(name: "row_version", table: "citas");

            migrationBuilder.AlterColumn<bool>(
                name: "activo",
                table: "usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProveedorId",
                table: "servicios",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "servicios",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Activo",
                table: "servicios",
                type: "bit",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "usuario_id",
                table: "clientes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "clientes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "modalidad",
                table: "citas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "fecha_creacion",
                table: "citas",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "fecha",
                table: "citas",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "citas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_proveedores_usuario_id",
                table: "proveedores",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_usuario_id",
                table: "clientes",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "FK_citas_clientes_cliente_id",
                table: "citas",
                column: "cliente_id",
                principalTable: "clientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_clientes_usuarios_usuario_id",
                table: "clientes",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_servicios_proveedores_ProveedorId",
                table: "servicios",
                column: "ProveedorId",
                principalTable: "proveedores",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}