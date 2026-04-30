using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class CitaCreateDto
    {
        // --- TUS CAMPOS ORIGINALES (INTACTOS) ---
        [Required]
        public Guid ClienteId { get; set; }
        [Required]
        public Guid ProveedorId { get; set; }
        [Required]
        public Guid ServicioId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        [Required]
        public TimeSpan Hora { get; set; }
        public string Modalidad { get; set; } = "local"; // "local" o "domicilio"
        public string? Direccion { get; set; }
        public string? Observaciones { get; set; }
        public decimal PrecioPactado { get; set; }
        public int DuracionPactadaMin { get; set; }

        // --- 🚩 ADICIONES PARA EL BOSS (QR & DOMICILIOS) ---
        
        // Para tracking: "QR", "Web", "Manual"
        [Required]
        public string MetodoRegistro { get; set; } = "Web";

        // Coordenadas para que el profesional use Google Maps/Waze
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }

        // Si el barbero cobra un extra por ir hasta la casa
        public decimal CostoDomicilio { get; set; } = 0;
    }
}