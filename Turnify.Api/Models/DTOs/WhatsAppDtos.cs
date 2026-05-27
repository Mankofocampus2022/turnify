using System;

namespace Turnify.Api.Models.DTOs
{
    // 📋 DTO para capturar el mensaje que entra al Webhook
    public class WhatsAppIncomingDto
    {
        public string Telefono { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    // 📋 DTO para estructurar la respuesta del Bot
    public class WhatsAppResponseDto
    {
        public string TelefonoCliente { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
        public DateTime FechaProcesado { get; set; }
    }
}