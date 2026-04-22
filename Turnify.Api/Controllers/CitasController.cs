using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims; // Necesario para extraer los Claims (ID del usuario) del JWT

namespace Turnify.Api.Controllers
{
    // Definición del controlador para la gestión de citas y agenda
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Bloqueo global: Todas las rutas requieren un token válido por defecto
    public class CitasController : ControllerBase
    {
        // Interfaz del servicio de citas inyectada vía constructor
        private readonly ICitaService _citaService;
        
        public CitasController(ICitaService citaService) => _citaService = citaService;

        // --- 📅 ENDPOINT: OBTENER AGENDA DE HOY ---
        // Se usa principalmente para la carga inicial del Dashboard
        [HttpGet("hoy")]
        public async Task<IActionResult> GetCitasHoy()
        {
            // 🛡️ Blindaje: Extraemos el ID del usuario directamente del Token para evitar suplantación
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // Si el token no tiene ID, la sesión es inválida o expiró
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            var userId = Guid.Parse(userIdClaim);
            
            // Invocamos al servicio para obtener la agenda filtrada por el día actual de Bogotá
            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            
            return Ok(agenda);
        }

        // --- 📊 ENDPOINT: OBTENER CITAS POR RANGO (NUEVO) ---
        // Permite filtrar la agenda por "Mañana", "Semana" o "Mes"
        [HttpGet("rango")]
        public async Task<IActionResult> GetCitasRango([FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            // 🛡️ Blindaje Senior: Validamos la identidad del usuario a través del Claim del JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida para esta consulta" });

            var userId = Guid.Parse(userIdClaim);
            
            // Llamamos al método de rangos que ya está blindado en la capa de Servicio (CitaService)
            // Este método ya maneja el AsNoTracking y el ordenamiento por fecha/hora
            var agenda = await _citaService.GetCitasRangoAsync(userId, inicio, fin);
            
            return Ok(agenda);
        }

        // --- 📝 ENDPOINT: CREAR NUEVA CITA ---
        [HttpPost("agendar")]
        [AllowAnonymous] // Excepción: Se permite agendar sin estar logueado (flujo de cliente final)
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            // Lógica de validación de horarios y disponibilidad en el servicio
            var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
            
            // Si el servicio detecta cruces o errores de negocio, devuelve BadRequest
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            
            return Ok(new { message = result.Message, citaId = result.CitaId });
        }

        // --- 🔍 ENDPOINT: CONSULTAR AGENDA POR PROVEEDOR ---
        [HttpGet("agenda/{proveedorId}")]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime fecha)
        {
            // Obtiene la agenda de un proveedor específico para una fecha determinada
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fecha);
            return Ok(agenda);
        }

        // --- 🕒 ENDPOINT: DISPONIBILIDAD DE HORARIOS ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] // Excepción: Clientes necesitan ver huecos libres antes de registrarse
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime fecha)
        {
            // Calcula los slots de tiempo libres restando las citas existentes del horario laboral
            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fecha);
            return Ok(slots);
        }

        // --- ⚡ ENDPOINT: ACTUALIZAR ESTADO DE LA CITA ---
        // Permite marcar como "Completada", "Cancelada", etc.
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> UpdateEstado(Guid id, [FromQuery] string nuevoEstado)
        {
            var result = await _citaService.UpdateEstadoCitaAsync(id, nuevoEstado);
            
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
                
            return Ok(new { message = result.Message });
        }

        // --- 📜 ENDPOINT: HISTORIAL DE CITAS DEL CLIENTE ---
        [HttpGet("historial/{clienteId}")]
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            // Recupera todas las citas pasadas y futuras de un cliente específico
            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            
            // Validación de contenido vacío para mejorar la respuesta del frontend
            if (historial == null || !historial.Any())
                return Ok(new { message = "Este cliente aún no tiene citas en su historial." });

            return Ok(historial);
        }
    }
}