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

        // 🚀 HU 001 - MULTI-SILLA: Tablas de la Épica
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
            modelBuilder.Entity<EstacionTrabajo>().Property(e => e.ValorBase).HasPrecision(18, 2);

            // 🚀 HU-09 & HU-12: Precisión para la comisión de proveedores dependientes
            modelBuilder.Entity<Proveedores>().Property(p => p.PorcentajeComision).HasPrecision(5, 2);

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
            // 3. MAPEO DETALLADO Y BLINDAJE PARA ESTACIONES DE TRABAJO (HU-001-B / C)
            // ============================================================================
            modelBuilder.Entity<EstacionTrabajo>(entity => {
                entity.ToTable("estaciones_trabajo");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ProveedorId).HasColumnName("proveedor_id");
                entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100);
                
                // 🛑 CORRECCIÓN CRÍTICA: Forzamos a EF a enviar explícitamente 'activo' en el INSERT 
                // para evitar el fallo por NULL en SQL Server.
                entity.Property(e => e.Activo)
                      .HasColumnName("activo")
                      .IsRequired();

                entity.Property(e => e.TipoCobro).HasColumnName("tipo_cobro").HasMaxLength(50);
                entity.Property(e => e.ValorBase).HasColumnName("valor_base");
                entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(20);

                // 🚀 Mapeo de Campos Temporales y Activación por Silla
                entity.Property(e => e.FechaVencimiento)
                      .HasColumnName("fecha_vencimiento")
                      .HasColumnType("datetimeoffset")
                      .IsRequired(false);

                entity.Property(e => e.Periodicidad)
                      .HasColumnName("periodicidad")
                      .HasMaxLength(50)
                      .IsRequired(false);
            });

            // ============================================================================
            // 4. INTEGRIDAD REFERENCIAL Y BLINDAJE DE CASCADAS (PROTECCIÓN MULTI-TENANT)
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

            // 🚀 RELACIÓN ESTACIÓN - EMPLEADO (Mapeada a la columna física 'empleado_id' en snake_case)
            modelBuilder.Entity<EstacionTrabajo>()
                .HasOne<Empleado>()
                .WithMany()
                .HasForeignKey("empleado_id")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

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
                      .HasForeignKey<Proveedores>(p => p.UsuarioId)
                      .IsRequired(false); // Permite proveedores sin un usuario asignado obligatorio
            });

            // 🛡️ BLINDAJE PARA CLIENTES
            modelBuilder.Entity<Clientes>(entity => {
                entity.Property(c => c.id).HasColumnName("id");
                entity.Property(c => c.usuario_id).HasColumnName("usuario_id");
            });

            // 🛡️ BLINDAJE Y EXTENSIÓN PARA PROVEEDORES (HU-08 a HU-12)
            modelBuilder.Entity<Proveedores>(entity => {
                entity.ToTable("proveedores");
                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.UsuarioId).HasColumnName("usuario_id").IsRequired(false);
                
                entity.Property(p => p.NombreComercial).HasColumnName("nombre_comercial");
                entity.Property(p => p.Direccion).HasColumnName("direccion");
                entity.Property(p => p.Tipo).HasColumnName("tipo");
                entity.Property(p => p.Categoria).HasColumnName("categoria");
                
                entity.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired(false);
                entity.Property(p => p.Email).HasColumnName("email").HasMaxLength(150).IsRequired(false); 

                // 🚀 Módulo de fotos, rol independiente y dependencias de Staff
                entity.Property(p => p.FotoUrl).HasColumnName("foto_url").HasMaxLength(500).IsRequired(false);
                entity.Property(p => p.EsIndependiente).HasColumnName("es_independiente").HasDefaultValue(false);
                entity.Property(p => p.StaffId).HasColumnName("staff_id").IsRequired(false);
                entity.Property(p => p.PorcentajeComision).HasColumnName("porcentaje_comision").HasDefaultValue(0.00m);

                // Relación opcional con el Staff/Dueño (Si el proveedor es dependiente)
                entity.HasOne(p => p.Staff)
                      .WithMany()
                      .HasForeignKey(p => p.StaffId)
                      .OnDelete(DeleteBehavior.SetNull);
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
            // 5. RELACIONES Y CLAVES FORÁNEAS DE CITAS
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
            // 6. CONFIGURACIÓN DE HORARIOS DE ATENCIÓN
            // ============================================================================
            modelBuilder.Entity<HorariosAtencion>()
                .HasOne(h => h.Proveedor)
                .WithMany(p => p.Horarios)
                .HasForeignKey(h => h.ProveedorId)
                .OnDelete(DeleteBehavior.NoAction);

            // ============================================================================
            // 7. DATOS SEMILLA (SEED DATA)
            // ============================================================================
            modelBuilder.Entity<Roles>().HasData(
                new Roles { id = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"), nombre = "Administrador" },
                new Roles { id = Guid.Parse("56992F75-6420-4D55-A5F9-9223248C50D7"), nombre = "Cliente" },
                new Roles { id = Guid.Parse("8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"), nombre = "Proveedor" },
                new Roles { id = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A"), nombre = "SuperAdministrador" },
                new Roles { id = Guid.Parse("99A2B3C4-E5F6-4789-90AB-C1D2E3F40099"), nombre = "Staff" }, // Rol Empleado / Colaborador
                new Roles { id = Guid.Parse("11B2C3D4-E5F6-7890-A1B2-C3D4E5F60010"), nombre = "ProveedorDependiente" }, // 🚀 HU-09: Colaborador en local
                new Roles { id = Guid.Parse("22C3D4E5-F6A7-8901-B2C3-D4E5F6A70020"), nombre = "ProveedorIndependiente" }  // 🚀 HU-10: Profesional a domicilio
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