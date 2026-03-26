using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("citas")]
    public class Citas
    {
        [Key]
        [Column("id")]
        // Opción recomendada: El servidor lo genera automáticamente al instanciar
        public Guid Id { get; set; } = Guid.NewGuid(); 

        [Required]
        [Column("cliente_id")]
        public Guid ClienteId { get; set; }

        [Required]
        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; }

        [Required]
        [Column("servicio_id")]
        public Guid ServicioId { get; set; }

        [Required]
        [Column("fecha")]
        public DateTime Fecha { get; set; } 

        [Required]
        [Column("hora")]
        public TimeSpan Hora { get; set; } 

        [Required]
        [StringLength(20)]
        [Column("modalidad")]
        public string Modalidad { get; set; } = "local";

        [StringLength(200)]
        [Column("direccion")]
        public string? Direccion { get; set; }

        [Required]
        [StringLength(20)]
        [Column("estado")]
        public string Estado { get; set; } = "pendiente";

        [StringLength(255)]
        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Required]
        [Column("precio_pactado")]
        public decimal PrecioPactado { get; set; } 

        [Column("duracion_pactada_min")]
        public int DuracionPactadaMin { get; set; }

        // --- RELACIONES (Navegación) ---
        [ForeignKey("ClienteId")]
        public virtual Clientes? Cliente { get; set; }

        [ForeignKey("ProveedorId")]
        public virtual Proveedores? Proveedor { get; set; }

        [ForeignKey("ServicioId")]
        public virtual Servicios? Servicio { get; set; }
    }
}