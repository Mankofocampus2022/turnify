using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class CitasController : ControllerBase
    {
        private readonly ICitaService _citaService;
        // 🚩 ROLE ARCHITECT: Inyectamos el DashboardService para la analítica avanzada
        private readonly IDashboardService _dashboardService;
        
        public CitasController(ICitaService citaService, IDashboardService dashboardService) 
        {
            _citaService = citaService;
            _dashboardService = dashboardService;
        }

        // --- 📅 ENDPOINT: OBTENER AGENDA DE HOY ---
        [HttpGet("hoy")]
        public async Task<IActionResult> GetCitasHoy()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            var userId = Guid.Parse(userIdClaim);
            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            return Ok(agenda);
        }

        // --- 📊 ENDPOINT: OBTENER CITAS POR RANGO ---
        [HttpGet("rango")]
        public async Task<IActionResult> GetCitasRango([FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida para esta consulta" });

            var userId = Guid.Parse(userIdClaim);
            var agenda = await _citaService.GetCitasRangoAsync(userId, inicio, fin);
            return Ok(agenda);
        }

        // 📈 --- NUEVO ENDPOINT: ANALÍTICA PARA GRÁFICAS (Chart.js) ---
        // Este es el que te dirá qué corte se hacen más o qué servicio de uñas domina
        [HttpGet("analitica-avanzada")]
        public async Task<IActionResult> GetAnaliticaAvanzada([FromQuery] Guid proveedorId, [FromQuery] string periodo = "mes", [FromQuery] DateTime? fecha = null)
        {
            // 🛡️ ROLE DBA: Llamamos al servicio que agrupó la data por servicios y clientes
            var analitica = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha, periodo);
            
            if (analitica == null) return NotFound(new { message = "No hay datos para este periodo" });

            return Ok(analitica);
        }

        // 📥 --- NUEVO ENDPOINT: EXPORTAR DATA (Para Excel/PDF) ---
        [HttpGet("exportar/datos")]
        public async Task<IActionResult> GetDatosParaExportar([FromQuery] Guid proveedorId, [FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            // Retorna la data plana pero ultra-detallada para que las librerías de JS generen el archivo
            var datos = await _citaService.GetCitasRangoAsync(proveedorId, inicio, fin);
            return Ok(datos);
        }

        // --- 📝 ENDPOINT: CREAR NUEVA CITA ---
        [HttpPost("agendar")]
        [AllowAnonymous] 
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message, citaId = result.CitaId });
        }

        // --- 🔍 ENDPOINT: CONSULTAR AGENDA POR PROVEEDOR ---
        [HttpGet("agenda/{proveedorId}")]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime fecha)
        {
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fecha);
            return Ok(agenda);
        }

        // --- 🕒 ENDPOINT: DISPONIBILIDAD DE HORARIOS ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime fecha)
        {
            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fecha);
            return Ok(slots);
        }

        // --- ⚡ ENDPOINT: ACTUALIZAR ESTADO DE LA CITA ---
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
            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            if (historial == null || !historial.Any())
                return Ok(new { message = "Este cliente aún no tiene citas en su historial." });
            return Ok(historial);
        }
    }
}