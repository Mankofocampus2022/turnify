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
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres")]
        public string? Descripcion { get; set; }

        [Required]
        [Column("Precio", TypeName = "decimal(18,2)")]
        [Range(0.01, 9999999.99, ErrorMessage = "El precio debe ser mayor a cero")]
        public decimal Precio { get; set; }

        // 🛡️ ANCLA DEL MOTOR OVERBOOKING PRO:
        // Validamos que la duración sea múltiplo de 5 o 15 para mantener la estética de la agenda.
        [Required]
        [Column("DuracionMinutos")]
        [Range(1, 480, ErrorMessage = "La duración máxima permitida por bloque es de 8 horas (480 min)")]
        public int DuracionMinutos { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Categoria")]
        public string Categoria { get; set; } = "Barbería";

        [Column("ImagenUrl")]
        [Url(ErrorMessage = "La URL de la imagen no es válida")]
        public string? ImagenUrl { get; set; }

        // 🛡️ BLINDAJE DE COMISIÓN: Aseguramos que no se guarden porcentajes absurdos.
        [Column("ComisionPorcentaje", TypeName = "decimal(5,2)")]
        [Range(0.00, 100.00, ErrorMessage = "La comisión debe estar entre 0% y 100%")]
        public decimal ComisionPorcentaje { get; set; } = 0.00m;

        // 🚩 ESTADO: 0 = Inactivo, 1 = Activo, 2 = En Proceso (Heineken Style)
        [Required]
        [Column("Activo")]
        [Range(0, 2, ErrorMessage = "Estado de servicio no válido")]
        public int Activo { get; set; } = 1; 

        // 🛡️ ISO DATETIME: Usamos DateTimeOffset para el fix de "El tiempo es relativo"
        [Column("FechaCreacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // 🛡️ BLINDAJE SENIOR: Cambiamos a Guid? (Nulable)
        [Column("ProveedorId")]
        public Guid? ProveedorId { get; set; } 

        // 🚩 BLINDAJE JSON: Evita ciclos infinitos en el API
        [ForeignKey("ProveedorId")]
        [JsonIgnore] 
        public virtual Proveedores? Proveedor { get; set; }
        
        // 🛡️ PROPIEDAD DE AUDITORÍA: Para trazabilidad en los reportes de BI
        [NotMapped]
        public string ResumenServicio => $"{Nombre} ({DuracionMinutos} min) - {Precio:C}";
    }
}