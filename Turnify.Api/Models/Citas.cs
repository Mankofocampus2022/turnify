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

        [Required(ErrorMessage = "El cliente es obligatorio")]
        [Column("cliente_id")]
        public Guid ClienteId { get; set; }

        [Required(ErrorMessage = "El proveedor es obligatorio")]
        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; }

        [Required(ErrorMessage = "El servicio es obligatorio")]
        [Column("servicio_id")]
        public Guid ServicioId { get; set; }

        [Required(ErrorMessage = "La fecha de la cita es obligatoria")]
        [Column("fecha")]
        public DateTime Fecha { get; set; } 

        [Required(ErrorMessage = "La hora de la cita es obligatoria")]
        [Column("hora")]
        public TimeSpan Hora { get; set; } 

        // 🚩 "local" o "domicilio"
        [Required]
        [StringLength(20)]
        [Column("modalidad")]
        [RegularExpression("local|domicilio", ErrorMessage = "La modalidad debe ser 'local' o 'domicilio'")]
        public string Modalidad { get; set; } = "local";

        [StringLength(200)]
        [Column("direccion")]
        public string? Direccion { get; set; }

        // 🚩 BLINDAJE GEOGRÁFICO: Precisión para Google Maps
        [Column("latitud", TypeName = "decimal(18, 10)")]
        [Range(-90, 90, ErrorMessage = "Latitud fuera de rango")]
        public decimal? Latitud { get; set; }

        [Column("longitud", TypeName = "decimal(18, 10)")]
        [Range(-180, 180, ErrorMessage = "Longitud fuera de rango")]
        public decimal? Longitud { get; set; }

        // 🚩 TRACKING: (QR, Web, Manual)
        [Required]
        [StringLength(20)]
        [Column("metodo_registro")]
        public string MetodoRegistro { get; set; } = "Web";

        // 🚩 ESTADOS: pendiente, confirmada, completada, cancelada, ausente
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
        [Column("precio_pactado", TypeName = "decimal(18, 2)")]
        [Range(0, 9999999.99)]
        public decimal PrecioPactado { get; set; } 

        // 🚩 COSTO EXTRA: Blindamos para que no sea negativo
        [Column("costo_domicilio", TypeName = "decimal(18, 2)")]
        [Range(0, 999999.99)]
        public decimal CostoDomicilio { get; set; } = 0;

        // 🛡️ ANCLA OVERBOOKING PRO: Duración real al momento de agendar
        [Required]
        [Column("duracion_pactada_min")]
        [Range(1, 480)]
        public int DuracionPactadaMin { get; set; }

        // 🛡️ BLINDAJE DE SEGURIDAD: Token de Check-in (6 dígitos)
        // Este campo asegura que el cliente llegó al local o recibió el domicilio.
        [Column("codigo_verificacion")]
        [StringLength(10)]
        public string? CodigoVerificacion { get; set; }

        // --- RELACIONES ---
        [ForeignKey("ClienteId")]
        public virtual Clientes? Cliente { get; set; }
        
        [ForeignKey("ProveedorId")]
        public virtual Proveedores? Proveedor { get; set; }
        
        [ForeignKey("ServicioId")]
        public virtual Servicios? Servicio { get; set; }
    }
}