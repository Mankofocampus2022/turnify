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
    // 🚩 DTO LOCAL: Mantenido para el PATCH
    public class EstadoUpdateDto 
    { 
        public string NuevoEstado { get; set; } 
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class CitasController : ControllerBase
    {
        private readonly ICitaService _citaService;
        private readonly IDashboardService _dashboardService;
        
        public CitasController(ICitaService citaService, IDashboardService dashboardService) 
        {
            _citaService = citaService;
            _dashboardService = dashboardService;
        }

        // --- 📅 1. AGENDA DE HOY ---
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

        // --- 📊 2. CITAS POR RANGO (Blindado contra fechas nulas) ---
        [HttpGet("rango")]
        public async Task<IActionResult> GetCitasRango([FromQuery] DateTime? inicio, [FromQuery] DateTime? fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida" });

            // 🛡️ BLINDAJE: Si no vienen fechas, usamos el día de hoy por defecto
            var fechaInicio = inicio ?? DateTime.Today;
            var fechaFin = fin ?? DateTime.Today;

            var userId = Guid.Parse(userIdClaim);
            var agenda = await _citaService.GetCitasRangoAsync(userId, fechaInicio, fechaFin);
            return Ok(agenda);
        }

        // 📈 --- 3. ANALÍTICA AVANZADA (Intacta) ---
        [HttpGet("analitica-avanzada")]
        public async Task<IActionResult> GetAnaliticaAvanzada([FromQuery] Guid proveedorId, [FromQuery] string periodo = "mes", [FromQuery] DateTime? fecha = null)
        {
            var analitica = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha, periodo);
            if (analitica == null) return NotFound(new { message = "No hay datos para este periodo" });
            return Ok(analitica);
        }

        // 📥 --- 4. EXPORTAR DATA ---
        [HttpGet("exportar/datos")]
        public async Task<IActionResult> GetDatosParaExportar([FromQuery] Guid proveedorId, [FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            var datos = await _citaService.GetCitasRangoAsync(proveedorId, inicio, fin);
            return Ok(datos);
        }

        // --- 📝 5. AGENDAR CITA (CORE) ---
        [HttpPost("agendar")]
        [AllowAnonymous] 
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            
            return Ok(new { 
                message = result.Message, 
                citaId = result.CitaId,
                modalidad = dto.Modalidad,
                registro = dto.MetodoRegistro 
            });
        }

        // --- 🔍 6. CONSULTAR AGENDA POR PROVEEDOR (Blindado) ---
        [HttpGet("agenda/{proveedorId}")]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            // 🛡️ Si la fecha llega nula o vacía desde el JS, forzamos a Hoy
            var fechaConsulta = fecha ?? DateTime.Today;
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fechaConsulta);
            return Ok(agenda);
        }

        // --- 🕒 7. DISPONIBILIDAD (EL KILLER FIX PARA EL BUG "SIN DISPONIBILIDAD") ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime? fecha)
        {
            // 🛡️ BLINDAJE DE SEGURIDAD:
            // Si el frontend envía una fecha vacía (""), .NET la recibe como nula o DateTime.MinValue.
            if (!fecha.HasValue || fecha.Value == DateTime.MinValue)
            {
                // En lugar de devolver error, intentamos salvar la petición usando el día de hoy
                fecha = DateTime.Today;
            }

            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fecha.Value);
            
            // Si no hay slots, devolvemos un mensaje claro para el log del JS
            if (slots == null || !slots.Any())
            {
                return Ok(new List<TimeSpan>()); // Devolvemos lista vacía pero con estatus 200
            }

            return Ok(slots);
        }

        // --- ⚡ 8. ACTUALIZAR ESTADO ---
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> UpdateEstado(Guid id, [FromBody] EstadoUpdateDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.NuevoEstado))
                return BadRequest(new { message = "El nuevo estado es requerido." });

            var result = await _citaService.UpdateEstadoCitaAsync(id, dto.NuevoEstado);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message });
        }

        // --- 📜 9. HISTORIAL ---
        [HttpGet("historial/{clienteId}")]
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            if (historial == null || !historial.Any())
                return Ok(new { message = "Este cliente aún no tiene citas en su historial." });
            return Ok(historial);
        }

        // --- 📍 10. UBICACIÓN ---
        [HttpGet("{id}/ubicacion")]
        public async Task<IActionResult> GetUbicacionDomicilio(Guid id)
        {
            // 🛡️ Usamos un rango amplio para buscar la cita específica
            var datos = await _citaService.GetCitasRangoAsync(Guid.Empty, DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1));
            var cita = datos.Cast<dynamic>().FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "Cita no encontrada" });
            
            return Ok(new {
                direccion = cita.Direccion,
                modalidad = cita.Modalidad
            });
        }
    }
}