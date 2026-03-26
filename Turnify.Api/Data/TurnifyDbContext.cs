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

            // 1. Configuración de Precisión Decimal (TU LÓGICA INTACTA)
            modelBuilder.Entity<Citas>().Property(c => c.PrecioPactado).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.Precio).HasPrecision(18, 2);
            modelBuilder.Entity<Servicios>().Property(s => s.ComisionPorcentaje).HasPrecision(5, 2); 
            modelBuilder.Entity<PlanSuscripcion>().Property(p => p.PrecioMensual).HasPrecision(18, 2);

            // 2. Mapeo de nombres de tablas (ASEGURAMOS TODAS EN MINÚSCULAS)
            modelBuilder.Entity<Roles>().ToTable("roles");
            modelBuilder.Entity<Usuarios>().ToTable("usuarios"); 
            modelBuilder.Entity<Proveedores>().ToTable("proveedores");
            modelBuilder.Entity<Servicios>().ToTable("servicios"); 
            modelBuilder.Entity<Clientes>().ToTable("clientes");
            modelBuilder.Entity<Citas>().ToTable("citas");
            modelBuilder.Entity<PlanSuscripcion>().ToTable("planes_suscripcion");
            modelBuilder.Entity<Suscripciones>().ToTable("suscripciones");
            modelBuilder.Entity<HorariosAtencion>().ToTable("horarios_atencion");

            // 🚩 MAPEO DE COLUMNAS PARA USUARIOS (Corregido a minúsculas para que compile)
            modelBuilder.Entity<Usuarios>(entity => {
                entity.ToTable("usuarios");
                // Usamos los nombres exactos de tu Usuarios.cs
                entity.Property(u => u.esta_bloqueado).HasColumnName("esta_bloqueado");
                entity.Property(u => u.suscripcion_fin).HasColumnName("suscripcion_fin");
                entity.Property(u => u.ultima_conexion).HasColumnName("ultima_conexion");
            });

            // 3. Relaciones de Citas (TU LÓGICA ORIGINAL INTACTA)
            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Proveedor)
                .WithMany()
                .HasForeignKey(c => c.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Citas>()
                .HasOne(c => c.Servicio)
                .WithMany()
                .HasForeignKey(c => c.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- 🚀 ESTO ES LO QUE PEGAMOS PARA ARREGLAR EL ERROR DEL SQL (INTACTO) ---
            modelBuilder.Entity<HorariosAtencion>()
                .HasOne(h => h.Proveedor)
                .WithMany(p => p.Horarios)
                .HasForeignKey(h => h.ProveedorId)
                .OnDelete(DeleteBehavior.NoAction);
            // ----------------------------------------------------------------

            // 4. DATOS SEMILLA - ROLES (TUS GUIDS SAGRADOS)
            modelBuilder.Entity<Roles>().HasData(
                new Roles { id = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"), nombre = "Administrador" },
                new Roles { id = Guid.Parse("56992F75-6420-4D55-A5F9-9223248C50D7"), nombre = "Cliente" },
                new Roles { id = Guid.Parse("8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"), nombre = "Proveedor" },
                new Roles { id = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A"), nombre = "SuperAdministrador" }
            );

            // 5. DATOS SEMILLA - PLANES (TUS PLANES ORIGINALES)
            modelBuilder.Entity<PlanSuscripcion>().HasData(
                new PlanSuscripcion { 
                    Id = Guid.Parse("D1A2B3C4-E5F6-4789-90AB-C1D2E3F40001"), 
                    Nombre = "Gratis", 
                    PrecioMensual = 0, 
                    LimiteCitasMes = 15, 
                    Activo = true 
                },
                new PlanSuscripcion { 
                    Id = Guid.Parse("E2F3A4B5-C6D7-4890-A1B2-C3D4E5F60002"), 
                    Nombre = "Premium", 
                    PrecioMensual = 19.99m, 
                    LimiteCitasMes = 9999, 
                    Activo = true 
                }
            );
        } 
    } 
}