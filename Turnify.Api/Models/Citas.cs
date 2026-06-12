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

        [Required(ErrorMessage = "La fecha de la cita es obligatoria")]
        [Column("fecha")]
        public DateTimeOffset Fecha { get; set; } // 📅 MIGRADO A DATETIMEOFFSET: Soporte internacional absoluto

        [Required(ErrorMessage = "La hora de la cita es obligatoria")]
        [Column("hora")]
        public TimeSpan Hora { get; set; } 

        // 🚩 MODIFICADO: Permitimos null para evitar el crash de SqlDataReader
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

        // 🚩 MODIFICADO: Blindaje contra nulos en DB
        [Column("metodo_registro")]
        public string? MetodoRegistro { get; set; } = "Web";

        // 🚩 MODIFICADO: El estado es el principal sospechoso del crash
        [Column("estado")]
        public string? Estado { get; set; } = "pendiente";

        [StringLength(255)]
        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("fecha_creacion")]
        public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow; // 📅 MIGRADO A DATETIMEOFFSET: Enrutado en UTC por defecto

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

        // 🛡️ SELLO DE CONCURRENCIA SENIOR (Bloqueo Optimista)
        [Timestamp]
        [Column("row_version")]
        public byte[] RowVersion { get; set; }

        // --- RELACIONES INTACTAS ---
        [ForeignKey("ClienteId")]
        public virtual Clientes? Cliente { get; set; }
        
        [ForeignKey("ProveedorId")]
        [InverseProperty("Citas")] 
        public virtual Proveedores? Proveedor { get; set; }
        
        [ForeignKey("ServicioId")]
        public virtual Servicios? Servicio { get; set; }
    }
}