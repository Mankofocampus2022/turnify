using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("clientes")]
    public class Clientes
    {
        [Key]
        public Guid id { get; set; }

        [Required]
        public Guid usuario_id { get; set; }

        [Required]
        [StringLength(120)]
        public string nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string telefono { get; set; } = string.Empty;

        [StringLength(150)]
        public string? email { get; set; }

        public bool activo { get; set; } = true;

        public DateTime fecha_creacion { get; set; } = DateTime.Now;

        // Relación con Usuarios
        [ForeignKey("usuario_id")]
        public virtual Usuarios? Usuario { get; set; }
    }
}