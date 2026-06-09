using System;
using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configuración de Precisión Decimal (INTACTA)
            modelBuilder.Entity<Citas>().Property(c => c.PrecioPactado).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.Precio).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.ComisionPorcentaje).HasPrecision(5, 2); 
            modelBuilder.Entity<PlanSuscripcion>().Property(p => p.PrecioMensual).HasPrecision(18, 2);

            // 2. Mapeo de nombres de tablas (MINÚSCULAS)
            modelBuilder.Entity<Roles>().ToTable("roles");
            modelBuilder.Entity<Usuarios>().ToTable("usuarios"); 
            modelBuilder.Entity<Proveedores>().ToTable("proveedores");
            modelBuilder.Entity<Servicios>().ToTable("servicios"); 
            modelBuilder.Entity<Clientes>().ToTable("clientes");
            modelBuilder.Entity<Citas>().ToTable("citas");
            modelBuilder.Entity<PlanSuscripcion>().ToTable("planes_suscripcion");
            modelBuilder.Entity<Suscripciones>().ToTable("suscripciones");
            modelBuilder.Entity<HorariosAtencion>().ToTable("horarios_atencion");

            // 🚩 MAPEO DE COLUMNAS PARA USUARIOS
            modelBuilder.Entity<Usuarios>(entity => {
                entity.Property(u => u.id).HasColumnName("id");
                entity.Property(u => u.email).HasColumnName("email");
                entity.Property(u => u.password_hash).HasColumnName("password_hash");
                entity.Property(u => u.rol_id).HasColumnName("rol_id");

                // 🛡️ RELACIÓN MAESTRA: Usuario -> Cliente (Minúsculas)
                entity.HasOne(u => u.Cliente)
                      .WithOne(c => c.Usuario)
                      .HasForeignKey<Clientes>(c => c.usuario_id);

                // 🛡️ RELACIÓN MAESTRA: Usuario -> Proveedor (PascalCase en Proveedores)
                // 🚩 FIX: Cambiamos p.usuario_id por p.UsuarioId
                entity.HasOne(u => u.Proveedor)
                      .WithOne(p => p.Usuario)
                      .HasForeignKey<Proveedores>(p => p.UsuarioId);
            });

            // 🛡️ BLINDAJE PARA CLIENTES (Minúsculas)
            modelBuilder.Entity<Clientes>(entity => {
                entity.Property(c => c.id).HasColumnName("id");
                entity.Property(c => c.usuario_id).HasColumnName("usuario_id");
            });

            // 🛡️ BLINDAJE PARA PROVEEDORES (Mapeo Estricto y Absoluto para Postgres)
            modelBuilder.Entity<Proveedores>(entity => {
                entity.ToTable("proveedores");
                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.UsuarioId).HasColumnName("usuario_id");
                
                // 🚩 FIX NUCLEAR: Mapeamos explícitamente TODAS las columnas para que EF no ignore ninguna en el UPDATE
                entity.Property(p => p.NombreComercial).HasColumnName("nombre_comercial");
                entity.Property(p => p.Direccion).HasColumnName("direccion");
                entity.Property(p => p.Tipo).HasColumnName("tipo");
                entity.Property(p => p.Categoria).HasColumnName("categoria");
                
                // Mantenemos la tolerancia de nulos pero forzamos el límite y el nombre exacto de la base de datos
                entity.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired(false);
                entity.Property(p => p.Email).HasColumnName("email").HasMaxLength(150).IsRequired(false); 
            });

            // 3. Relaciones de Citas (🚩 FIX MAESTRO PARA ELIMINAR 'ProveedoresId')
            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Proveedor)
                .WithMany(p => p.Citas) // 🛡️ Mapeo explícito a la colección en Proveedores
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

            // 🛡️ MAPEO DE CONCURRENCIA SENIOR (RowVersion)
            // Vincula la propiedad binaria con la columna física y activa el rastreo de tokens de EF Core
            modelBuilder.Entity<Citas>()
                .Property(c => c.RowVersion)
                .HasColumnName("row_version")
                .IsRowVersion();

            // 🚀 CONFIGURACIÓN DE HORARIOS
            modelBuilder.Entity<HorariosAtencion>()
                .HasOne(h => h.Proveedor)
                .WithMany(p => p.Horarios)
                .HasForeignKey(h => h.ProveedorId)
                .OnDelete(DeleteBehavior.NoAction);

            // 4. DATOS SEMILLA (TUS GUIDS SAGRADOS)
            modelBuilder.Entity<Roles>().HasData(
                new Roles { id = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"), nombre = "Administrador" },
                new Roles { id = Guid.Parse("56992F75-6420-4D55-A5F9-9223248C50D7"), nombre = "Cliente" },
                new Roles { id = Guid.Parse("8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"), nombre = "Proveedor" },
                new Roles { id = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A"), nombre = "SuperAdministrador" }
            );

            // 5. DATOS SEMILLA - PLANES
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