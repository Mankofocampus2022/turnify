using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models
{
    [Table("estaciones_trabajo")]
    public class EstacionTrabajo
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        // --- CAMPOS FINANCIEROS Y OPERATIVOS ---
        [Column("tipo_cobro")]
        [StringLength(50)]
        public string TipoCobro { get; set; } = "Porcentaje";

        [Column("valor_base", TypeName = "decimal(18,2)")]
        public decimal ValorBase { get; set; } = 0;

        [Column("estado")]
        [StringLength(20)]
        public string Estado { get; set; } = "Disponible";

        // --- 🚀 CAMPOS AGREGADOS PARA ACTIVACIÓN Y CONTROL TEMPORAL (HU-001-B / C) ---
        [Column("fecha_vencimiento")]
        public DateTimeOffset? FechaVencimiento { get; set; }

        [Column("periodicidad")]
        [StringLength(50)]
        public string? Periodicidad { get; set; }

        // --- RELACIÓN ORIGINAL INTACTA ---
        [ForeignKey("ProveedorId")]
        [JsonIgnore]
        public virtual Proveedores? Proveedor { get; set; }
    }
}