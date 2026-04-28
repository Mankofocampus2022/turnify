using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization; 

namespace Turnify.Api.Models
{
    [Table("servicios")]
    public class Servicios
    {
        [Key]
        public Guid Id { get; set; }

        [Required(ErrorMessage = "El nombre del servicio es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [Column("Precio", TypeName = "decimal(18,2)")]
        [Range(0, 9999999.99, ErrorMessage = "El precio debe ser un valor positivo")]
        public decimal Precio { get; set; }

        [Required]
        [Column("DuracionMinutos")]
        [Range(1, 1440, ErrorMessage = "La duración debe ser al menos de 1 minuto")]
        public int DuracionMinutos { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Categoria")]
        public string Categoria { get; set; } = "Barbería";

        [Column("ImagenUrl")]
        public string? ImagenUrl { get; set; }

        [Column("ComisionPorcentaje", TypeName = "decimal(5,2)")]
        public decimal ComisionPorcentaje { get; set; } = 0.00m;

        // 🚩 ESTADO: 0 = Inactivo, 1 = Activo, 2 = En Proceso (Heineken Style)
        [Required]
        [Column("Activo")]
        public int Activo { get; set; } = 1; 

        [Column("FechaCreacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // 🛡️ BLINDAJE SENIOR: Cambiamos a Guid? (Nulable)
        // Quitamos el [Required] para que el SQL no rechace el registro si el SuperAdmin manda un null.
        [Column("ProveedorId")]
        public Guid? ProveedorId { get; set; } 

        // 🚩 BLINDAJE JSON: Evita ciclos infinitos en el API
        [ForeignKey("ProveedorId")]
        [JsonIgnore] 
        public virtual Proveedores? Proveedor { get; set; }
    }
}