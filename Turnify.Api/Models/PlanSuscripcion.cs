using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models
{
    public class PlanSuscripcion
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty; // Básico, Pro, etc.
        
        [Required]
        public decimal PrecioMensual { get; set; }
        
        public int? LimiteCitasMes { get; set; } // Null para ilimitado
        
        public bool Activo { get; set; } = true;
    }
}