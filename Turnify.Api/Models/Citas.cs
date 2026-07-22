using System;
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

        // 🚀 HU 001 - MULTI-SILLA Y AGENCIAS (NULABLES)
        [Column("empleado_id")]
        public Guid? EmpleadoId { get; set; }

        [Column("estacion_id")]
        public Guid? EstacionId { get; set; }

        [Required(ErrorMessage = "La fecha de la cita es obligatoria")]
        [Column("fecha")]
        public DateTimeOffset Fecha { get; set; } 

        [Required(ErrorMessage = "La hora de la cita es obligatoria")]
        [Column("hora")]
        public TimeSpan Hora { get; set; } 

        [Column("modalidad")]
        public string? Modalidad { get; set; } = "local";

        [StringLength(200)]
        [Column("direccion")]
        public string? Direccion { get; set; }

        [Column("latitud", TypeName = "decimal(18, 10)")]
        [Range(-90, 90, ErrorMessage = "Latitud fuera de rango")]
        public decimal? Latitud { get; set; }

        [Column("longitud", TypeName = "decimal(18, 10)")]
        [Range(-180, 180, ErrorMessage = "Longitud fuera de rango")]
        public decimal? Longitud { get; set; }

        [Column("metodo_registro")]
        public string? MetodoRegistro { get; set; } = "Web";

        [Column("estado")]
        public string? Estado { get; set; } = "pendiente";

        [StringLength(255)]
        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("fecha_creacion")]
        public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow; 

        [Required]
        [Column("precio_pactado", TypeName = "decimal(18, 2)")]
        [Range(0, 9999999.99)]
        public decimal PrecioPactado { get; set; } 

        [Column("costo_domicilio", TypeName = "decimal(18, 2)")]
        [Range(0, 999999.99)]
        public decimal CostoDomicilio { get; set; } = 0;

        [Required]
        [Column("duracion_pactada_min")]
        [Range(1, 480)]
        public int DuracionPactadaMin { get; set; }

        [Column("codigo_verificacion")]
        [StringLength(10)]
        public string? CodigoVerificacion { get; set; }

        // --- CORRECCIÓN CS8618: Inicialización limpia para control de concurrencia ---
        [Timestamp]
        [Column("row_version")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // --- RELACIONES INTACTAS Y NUEVAS ---
        [ForeignKey("ClienteId")]
        public virtual Clientes? Cliente { get; set; }
        
        [ForeignKey("ProveedorId")]
        [InverseProperty("Citas")] 
        public virtual Proveedores? Proveedor { get; set; }
        
        [ForeignKey("ServicioId")]
        public virtual Servicios? Servicio { get; set; }

        // 🚀 Relaciones Multi-Tenant 2.0
        [ForeignKey("EmpleadoId")]
        public virtual Empleado? Empleado { get; set; }

        [ForeignKey("EstacionId")]
        public virtual EstacionTrabajo? Estacion { get; set; }
    }
}