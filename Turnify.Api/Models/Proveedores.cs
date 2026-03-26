using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Para que reconozca DeleteBehavior si fuera necesario aquí

namespace Turnify.Api.Models
{
    [Table("proveedores")]
    public class Proveedores
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("usuario_id")]
        public Guid UsuarioId { get; set; }

        [Required]
        [Column("tipo")]
        public string Tipo { get; set; } = "negocio";

        [Required]
        [Column("nombre_comercial")] 
        public string NombreComercial { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("direccion")]
        public string Direccion { get; set; } = string.Empty;

        [Column("ciudad")]
        public string? Ciudad { get; set; }

        [Column("trabaja_domicilio")]
        public bool TrabajaDomicilio { get; set; } = false;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("eliminado")]
        public bool Eliminado { get; set; } = false;

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column("fecha_actualizacion")]
        public DateTime? FechaActualizacion { get; set; }

        // --- RELACIONES ---

        [ForeignKey("UsuarioId")]
        public virtual Usuarios? Usuario { get; set; }
        
        // Relación con Horarios: Un proveedor tiene MUCHOS horarios
        public virtual ICollection<HorariosAtencion> Horarios { get; set; } = new List<HorariosAtencion>();

        public virtual ICollection<Suscripciones> Suscripciones { get; set; } = new List<Suscripciones>();
    }
}