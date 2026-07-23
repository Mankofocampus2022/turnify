using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion; 
using Turnify.Api.Models; 

namespace Turnify.Api.Data
{
    public class TurnifyDbContext : DbContext
    {
        public TurnifyDbContext(DbContextOptions<TurnifyDbContext> options) : base(options) { }

        public DbSet<Roles> roles { get; set; }
        public DbSet<Usuarios> usuarios { get; set; }
        public DbSet<Proveedores> proveedores { get; set; }
        public DbSet<Servicios> servicios { get; set; }
        public DbSet<Clientes> clientes { get; set; }
        public DbSet<Citas> citas { get; set; }

        public DbSet<PlanSuscripcion> planes_suscripcion { get; set; }
        public DbSet<Suscripciones> suscripciones { get; set; }
        public DbSet<HorariosAtencion> horarios_atencion { get; set; }

        // 🚀 HU 001 - MULTI-SILLA: Nuevas tablas
        public DbSet<EstacionTrabajo> estaciones_trabajo { get; set; }
        public DbSet<Empleado> empleados { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================================================
            // 1. CONFIGURACIÓN DE PRECISIÓN DECIMAL
            // ============================================================================
            modelBuilder.Entity<Citas>().Property(c => c.PrecioPactado).HasPrecision(18, 2);
            modelBuilder.Entity<Citas>().Property(c => c.CostoDomicilio).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.Precio).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.ComisionPorcentaje).HasPrecision(5, 2); 
            modelBuilder.Entity<PlanSuscripcion>().Property(p => p.PrecioMensual).HasPrecision(18, 2);
            
            // 🚀 HU 001: Precisión para el valor del contrato/pago silla/comisión
            modelBuilder.Entity<Empleado>().Property(e => e.ValorContrato).HasPrecision(18, 2);

            // ============================================================================
            // 2. MAPEO DE NOMBRES DE TABLAS (MINÚSCULAS Y SNAKE_CASE)
            // ============================================================================
            modelBuilder.Entity<Roles>().ToTable("roles");
            modelBuilder.Entity<Usuarios>().ToTable("usuarios"); 
            modelBuilder.Entity<Proveedores>().ToTable("proveedores");
            modelBuilder.Entity<Servicios>().ToTable("servicios"); 
            modelBuilder.Entity<Clientes>().ToTable("clientes");
            modelBuilder.Entity<Citas>().ToTable("citas");
            modelBuilder.Entity<PlanSuscripcion>().ToTable("planes_suscripcion");
            modelBuilder.Entity<Suscripciones>().ToTable("suscripciones");
            modelBuilder.Entity<HorariosAtencion>().ToTable("horarios_atencion");
            modelBuilder.Entity<Empleado>().ToTable("empleados");
            modelBuilder.Entity<EstacionTrabajo>().ToTable("estaciones_trabajo");

            // ============================================================================
            // 3. INTEGRIDAD REFERENCIAL Y BLINDAJE DE CASCADAS (PROTECCIÓN MULTI-TENANT)
            // ============================================================================

            // 🚩 FIX NUCLEAR 1: Evita borrado en cascada de Proveedor -> Estaciones
            modelBuilder.Entity<EstacionTrabajo>()
                .HasOne(e => e.Proveedor)
                .WithMany()
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict); 
            
            // 🚩 FIX NUCLEAR 2: Evita borrado en cascada de Proveedor -> Empleados
            modelBuilder.Entity<Empleado>()
                .HasOne(e => e.Proveedor)
                .WithMany()
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🚀 RELACIÓN ESTACIÓN - EMPLEADO (Mapeada mediante sombra/unidireccional segura para evitar CS1061)
            modelBuilder.Entity<EstacionTrabajo>()
                .HasOne<Empleado>()
                .WithMany()
                .HasForeignKey("EmpleadoId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull); // Si se borra el empleado, la estación no se elimina, solo queda libre

            // 🚩 MAPEO DE COLUMNAS Y RELACIONES DE USUARIOS
            modelBuilder.Entity<Usuarios>(entity => {
                entity.Property(u => u.id).HasColumnName("id");
                entity.Property(u => u.email).HasColumnName("email");
                entity.Property(u => u.password_hash).HasColumnName("password_hash");
                entity.Property(u => u.rol_id).HasColumnName("rol_id");

                entity.HasOne(u => u.Cliente)
                      .WithOne(c => c.Usuario)
                      .HasForeignKey<Clientes>(c => c.usuario_id);

                entity.HasOne(u => u.Proveedor)
                      .WithOne(p => p.Usuario)
                      .HasForeignKey<Proveedores>(p => p.UsuarioId);
            });

            // 🛡️ BLINDAJE PARA CLIENTES
            modelBuilder.Entity<Clientes>(entity => {
                entity.Property(c => c.id).HasColumnName("id");
                entity.Property(c => c.usuario_id).HasColumnName("usuario_id");
            });

            // 🛡️ BLINDAJE PARA PROVEEDORES
            modelBuilder.Entity<Proveedores>(entity => {
                entity.ToTable("proveedores");
                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.UsuarioId).HasColumnName("usuario_id");
                
                entity.Property(p => p.NombreComercial).HasColumnName("nombre_comercial");
                entity.Property(p => p.Direccion).HasColumnName("direccion");
                entity.Property(p => p.Tipo).HasColumnName("tipo");
                entity.Property(p => p.Categoria).HasColumnName("categoria");
                
                entity.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired(false);
                entity.Property(p => p.Email).HasColumnName("email").HasMaxLength(150).IsRequired(false); 
            });

            // ============================================================================
            // 🌐 ALINEACIÓN GLOBAL NATIVA (DATETIMEOFFSET & CONCURRENCIA)
            // ============================================================================
            modelBuilder.Entity<Citas>()
                .Property(c => c.Fecha)
                .HasColumnType("datetimeoffset");

            modelBuilder.Entity<Citas>()
                .Property(c => c.FechaCreacion)
                .HasColumnType("datetimeoffset");

            // 🛡️ MAPEO DE CONCURRENCIA SENIOR (RowVersion)
            modelBuilder.Entity<Citas>()
                .Property(c => c.RowVersion)
                .HasColumnName("row_version")
                .IsRowVersion();

            // ============================================================================
            // 4. RELACIONES Y CLAVES FORÁNEAS DE CITAS
            // ============================================================================
            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Proveedor)
                .WithMany(p => p.Citas) 
                .HasForeignKey(c => c.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Servicio)
                .WithMany()
                .HasForeignKey(c => c.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Cliente)
                .WithMany(cl => cl.Citas)
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // 🚀 HU 001 - RELACIONES DE CITAS CON EMPLEADO Y ESTACIÓN DE TRABAJO
            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Empleado)
                .WithMany()
                .HasForeignKey(c => c.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Estacion)
                .WithMany()
                .HasForeignKey(c => c.EstacionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================================
            // 5. CONFIGURACIÓN DE HORARIOS DE ATENCIÓN
            // ============================================================================
            modelBuilder.Entity<HorariosAtencion>()
                .HasOne(h => h.Proveedor)
                .WithMany(p => p.Horarios)
                .HasForeignKey(h => h.ProveedorId)
                .OnDelete(DeleteBehavior.NoAction);

            // ============================================================================
            // 6. DATOS SEMILLA (SEED DATA)
            // ============================================================================
            modelBuilder.Entity<Roles>().HasData(
                new Roles { id = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"), nombre = "Administrador" },
                new Roles { id = Guid.Parse("56992F75-6420-4D55-A5F9-9223248C50D7"), nombre = "Cliente" },
                new Roles { id = Guid.Parse("8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"), nombre = "Proveedor" },
                new Roles { id = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A"), nombre = "SuperAdministrador" },
                new Roles { id = Guid.Parse("99A2B3C4-E5F6-4789-90AB-C1D2E3F40099"), nombre = "Staff" } // Rol Empleado / Colaborador
            );

            modelBuilder.Entity<PlanSuscripcion>().HasData(
                new PlanSuscripcion { 
                    Id = Guid.Parse("D1A2B3C4-E5F6-4789-90AB-C1D2E3F40001"), 
                    Nombre = "Gratis", PrecioMensual = 0, LimiteCitasMes = 15, Activo = true 
                },
                new PlanSuscripcion { 
                    Id = Guid.Parse("E2F3A4B5-C6D7-4890-A1B2-C3D4E5F60002"), 
                    Nombre = "Premium", PrecioMensual = 19.99m, LimiteCitasMes = 9999, Activo = true 
                }
            );
        } 
    } 
}