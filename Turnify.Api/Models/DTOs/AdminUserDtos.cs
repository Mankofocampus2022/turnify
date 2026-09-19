using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.DTOs
{
    public class SystemUserDto
    {
        public Guid UserId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = "No registrado";
        public string RolNombre { get; set; } = "Sin Rol";
        public string ProveedorNombre { get; set; } = "N/A";
        public string PlanNombre { get; set; } = "Sin Plan";
        public string EstadoSuscripcion { get; set; } = "Inactivo";
        
        // 🚀 CORRECCIÓN: Se cambia DateTime? por DateTimeOffset? para solucionar el error de Cast
        public DateTimeOffset? FechaVencimiento { get; set; }
        
        public int DiasRestantes { get; set; }
        public bool Activo { get; set; }
    }

    public class ManualPaymentDto
    {
        [Required]
        public string PaymentMethod { get; set; } = "Nequi";

        public string ReferenceNumber { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Range(1, 365)]
        public int ExtensionPeriodDays { get; set; } = 30;

        public string? Notes { get; set; }
    }

    public class ExtendServiceDto
    {
        [Range(1, 365)]
        public int AdditionalDays { get; set; } = 7;

        public string? Reason { get; set; }
    }
}