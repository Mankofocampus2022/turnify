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

        // =================================================================
        // 📡 ENDPOINT 1: VERIFICACIÓN DEL WEBHOOK (Meta Get)
        // =================================================================
        [HttpGet("webhook")]
        public IActionResult VerificarWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            const string tokenSeguroLocal = "Turnify.Bot.Token.Master.2026"; 

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
        public async Task<IActionResult> RecibirMensaje([FromBody] WhatsAppIncomingDto payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Telefono) || string.IsNullOrWhiteSpace(payload.Mensaje))
            {
                return BadRequest(new { error = "El número de teléfono y el mensaje son obligatorios." });
            }

            _logger.LogInformation($"📥 [WhatsApp Inbound] Mensaje recibido de {payload.Telefono}: {payload.Mensaje}");

            try
            {
                // Enviamos el mensaje a nuestra máquina de estados conversacional en C#
                string respuestaBot = await _whatsAppService.ProcesarMensajeEntranteAsync(payload.Telefono, payload.Mensaje);

                return Ok(new WhatsAppResponseDto
                {
                    TelefonoCliente = payload.Telefono,
                    Respuesta = respuestaBot,
                    FechaProcesado = DateTime.Now
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