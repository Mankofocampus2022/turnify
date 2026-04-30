using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("citas")]
    public class Citas
    {
        [Key]
        [Column("id")]
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

        // 🚩 "local" o "domicilio"
        [Required]
        [StringLength(20)]
        [Column("modalidad")]
        public string Modalidad { get; set; } = "local";

        [StringLength(200)]
        [Column("direccion")]
        public string? Direccion { get; set; }

        // 🚩 NUEVO: Para Google Maps mañana
        [Column("latitud", TypeName = "decimal(18, 10)")]
        public decimal? Latitud { get; set; }

        [Column("longitud", TypeName = "decimal(18, 10)")]
        public decimal? Longitud { get; set; }

        // 🚩 NUEVO: Tracking del QR (QR, Web, Manual)
        [Required]
        [StringLength(20)]
        [Column("metodo_registro")]
        public string MetodoRegistro { get; set; } = "Web";

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

        // 🚩 NUEVO: Extra por domicilio
        [Column("costo_domicilio")]
        public decimal CostoDomicilio { get; set; } = 0;

        [Column("duracion_pactada_min")]
        public int DuracionPactadaMin { get; set; }

        // --- RELACIONES ---
        [ForeignKey("ClienteId")]
        public virtual Clientes? Cliente { get; set; }
        [ForeignKey("ProveedorId")]
        public virtual Proveedores? Proveedor { get; set; }
        [ForeignKey("ServicioId")]
        public virtual Servicios? Servicio { get; set; }
    }
}