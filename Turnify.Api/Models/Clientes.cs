using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("clientes")]
    public class Clientes
    {
        [Key]
        [Column("id")] // 🛡️ Mapeo explícito
        public Guid id { get; set; }

        [Required]
        [Column("usuario_id")] // 🛡️ Mapeo explícito
        public Guid usuario_id { get; set; }

        [Required]
        [StringLength(120)]
        [Column("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Column("telefono")] // 🛡️ Esto asegura que el "311..." se guarde donde debe
        public string telefono { get; set; } = string.Empty;

        [Required] // 🚩 CAMBIO: Lo hacemos requerido para que siempre haya data para validar
        [StringLength(150)]
        [Column("email")] // 🚩 ESENCIAL: Forzamos el mapeo a la columna física de SQL
        public string email { get; set; } = string.Empty;

        [Column("activo")]
        public bool activo { get; set; } = true;

        [Column("fecha_creacion")]
        public DateTime fecha_creacion { get; set; } = DateTime.Now;

        // Relación con Usuarios
        [ForeignKey("usuario_id")]
        public virtual Usuarios? Usuario { get; set; }
    }
}