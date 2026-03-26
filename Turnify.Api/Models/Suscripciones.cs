using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turnify.Api.Models
{
    public class Suscripciones
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProveedorId { get; set; }
        
        [ForeignKey("ProveedorId")]
        public Proveedores? Proveedor { get; set; }

        [Required]
        public Guid PlanId { get; set; }
        
        [ForeignKey("PlanId")]
        public PlanSuscripcion? Plan { get; set; }

        public DateTime FechaInicio { get; set; } = DateTime.Now;
        
        [Required]
        public DateTime FechaVencimiento { get; set; }
        
        public string Estado { get; set; } = "Activo"; // Activo, Vencido, Pendiente
    }
}