using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("servicios")]
    public class Servicios
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [Column("Precio", TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Required]
        [Column("DuracionMinutos")]
        public int DuracionMinutos { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Categoria")]
        public string Categoria { get; set; } = "Barbería";

        [Column("ImagenUrl")]
        public string? ImagenUrl { get; set; }

        [Column("ComisionPorcentaje", TypeName = "decimal(5,2)")]
        public decimal ComisionPorcentaje { get; set; } = 0.00m;

        // 🚩 CAMBIO CLAVE: Ahora es INT para soportar 0, 1 y 2
        [Column("Activo")]
        public int Activo { get; set; } = 1; 

        [Column("FechaCreacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("ProveedorId")]
        public Guid ProveedorId { get; set; }

        [ForeignKey("ProveedorId")]
        public virtual Proveedores? Proveedor { get; set; }
    }
}