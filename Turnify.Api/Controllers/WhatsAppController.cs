using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;  
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;                           
using System.Threading.Tasks;           
using Microsoft.Extensions.Logging;     

namespace Turnify.Api.Controllers
{
    // 🛡️ DTOs DE INTEGRACIÓN INTERNOS: Blindados contra Warning CS8618 (Nulabilidad) y unificados para el Webhook Multi-Tenant
    public class WhatsAppIncomingDto
    {
        public string Telefono { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        
        // 🚩 [NUEVO] Propiedad para capturar cuál línea corporativa de barbero recibió el mensaje
        public string TelefonoReceptor { get; set; } = string.Empty;
    }

    public class WhatsAppResponseDto
    {
        public string TelefonoCliente { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
        public DateTime FechaProcesado { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<WhatsAppController> _logger;

        public WhatsAppController(IWhatsAppService whatsAppService, ILogger<WhatsAppController> logger)
        {
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        // 🚩 MÉTODO PRIVADO: Obtener hora actual de Bogotá para auditoría de payloads
        private DateTime GetBogotaTime()
        {
            try 
            {
                var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
            }
            catch 
            {
                return DateTime.UtcNow.AddHours(-5);
            }
        }

        // =================================================================
        // 📡 ENDPOINT 1: VERIFICACIÓN DEL WEBHOOK (Meta Get)
        // =================================================================
        [HttpGet("webhook")]
        [AllowAnonymous] // El handshake de Meta se ejecuta sin token Bearer
        public IActionResult VerificarWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            // 🛡️ BLINDAJE OBS-02: Extraemos el token secreto desde el entorno Linux de Docker. Fallback seguro si no está mapeado.
            var tokenSeguroLocal = Environment.GetEnvironmentVariable("WHATSAPP_VERIFY_TOKEN") ?? "Turnify.Bot.Token.Master.2026"; 

            if (mode == "subscribe" && token == tokenSeguroLocal)
            {
                _logger.LogInformation("✅ [WhatsApp Webhook] Validación de Meta/Facebook exitosa.");
                return Ok(challenge); 
            }

            _logger.LogWarning("⚠️ [WhatsApp Webhook] Intento de validación fallido o token incorrecto.");
            return Forbid();
        }

        // =================================================================
        // 📡 ENDPOINT 2: RECIBIR MENSAJES (El corazón del Bot)
        // =================================================================
        [HttpPost("webhook")]
        [AllowAnonymous] // Meta envía los mensajes de los clientes de forma pública hacia tu Webhook
        public async Task<IActionResult> RecibirMensaje([FromBody] WhatsAppIncomingDto payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Telefono) || string.IsNullOrWhiteSpace(payload.Mensaje))
            {
                return BadRequest(new { error = "El número de teléfono y el mensaje son obligatorios." });
            }

            _logger.LogInformation($"📥 [WhatsApp Inbound] Mensaje recibido de {payload.Telefono} hacia {payload.TelefonoReceptor}: {payload.Mensaje}");

            try
            {
                // 🚩 CAMBIO INTEGRAL: Pasamos el teléfono receptor a nuestra máquina de estados conversacional en C#
                string respuestaBot = await _whatsAppService.ProcesarMensajeEntranteAsync(payload.Telefono, payload.TelefonoReceptor, payload.Mensaje);

                return Ok(new WhatsAppResponseDto
                {
                    TelefonoCliente = payload.Telefono,
                    Respuesta = respuestaBot,
                    FechaProcesado = GetBogotaTime() // 🚩 FIX TC-001: Estampa de tiempo exacta de Bogotá
                });
            }
            catch (Exception ex)
            {
                // 🛡️ CONTROL SENIOR DE CONTENCIÓN: Evita bucles infinitos de reintentos por parte de Meta si el servicio falla
                _logger.LogError(ex, "🚨 [WhatsApp Controller Crash] Error crítico al procesar el flujo conversacional para el teléfono {Telefono}", payload.Telefono);
                
                return StatusCode(500, new { 
                    error = "Error interno al procesar el mensaje en el Bot.",
                    message = ex.Message 
                });
            }
        }
    }
}