using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    [Table("horarios_atencion")]
    public class HorariosAtencion
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("proveedor_id")]
        public Guid ProveedorId { get; set; }

        [Required]
        [Column("dia_semana")]
        public int DiaSemana { get; set; }

        [Required]
        [Column("hora_apertura")]
        public TimeSpan HoraApertura { get; set; }

        [Required]
        [Column("hora_cierre")]
        public TimeSpan HoraCierre { get; set; }

        // --- EL AMARRE TÉCNICO ---
        // Esto le dice a EF: "Oye, ProveedorId es la llave para llegar a la tabla Proveedores"
        [ForeignKey("ProveedorId")]
        public virtual Proveedores? Proveedor { get; set; }
    }
}