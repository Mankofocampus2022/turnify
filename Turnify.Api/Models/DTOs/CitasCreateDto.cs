using System;
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

        // --- 🛡️ BLINDAJE EXTRA PARA MULTI-NEGOCIO (No daña funcionalidad) ---

        // 🚩 [NUEVO] Para validar si el barbero está agendando desde su propia cuenta 
        // o si es un cliente externo. Esto evita que las citas de "Tola y Maruja 2" 
        // se mezclen con otros locales del mismo dueño.
        public Guid? UsuarioCreadorId { get; set; }

        // 🚩 [NUEVO] Versión del DTO para asegurar compatibilidad con el JSON del Front
        public string? VersionApp { get; set; } = "1.0.2";

        // --- 🚀 [NUEVO GUEST CHECKOUT] - BLINDAJE PARA CLIENTES ANÓNIMOS (INVITADOS QR) ---
        // Permite capturar los datos de contacto de clientes no registrados en la base de datos
        // para la auto-creación del perfil en caliente y el envío automático del token.
        public string? AnonimoNombre { get; set; }
        public string? AnonimoEmail { get; set; }
        public string? AnonimoWhatsApp { get; set; }
    }
}