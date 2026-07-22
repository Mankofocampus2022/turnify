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

        // --- NUEVOS CAMPOS AGREGADOS ---
        [Column("tipo_cobro")]
        [StringLength(50)]
        public string TipoCobro { get; set; } = "Porcentaje";

        [Column("valor_base", TypeName = "decimal(18,2)")]
        public decimal ValorBase { get; set; } = 0;

        [Column("estado")]
        [StringLength(20)]
        public string Estado { get; set; } = "Disponible";

        // --- RELACIÓN ORIGINAL INTACТА ---
        [ForeignKey("ProveedorId")]
        [JsonIgnore]
        public virtual Proveedores? Proveedor { get; set; }
    }
}