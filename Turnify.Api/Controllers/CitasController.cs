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
    // 🚩 DTOs LOCALES: Blindados contra Warning CS8618 (Nulabilidad)
    public class EstadoUpdateDto 
    { 
        public string NuevoEstado { get; set; } = string.Empty; 
    }

    public class CheckInDto
    {
        public Guid CitaId { get; set; }
        public string Token { get; set; } = string.Empty;
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

        // --- 📅 1. AGENDA DE HOY (Filtro Estricto) ---
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
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // 🛡️ Blindaje: Solo el proveedor o un Admin pueden ver esta analítica
            if (proveedorId != Guid.Empty && userIdClaim != proveedorId.ToString() && !User.IsInRole("Admin"))
                return Forbid();

            var analitica = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha, periodo);
            if (analitica == null) return NotFound(new { message = "No hay datos para este periodo" });
            return Ok(analitica);
        }

        // 📥 --- 4. EXPORTAR DATA ---
        [HttpGet("exportar/datos")]
        public async Task<IActionResult> GetDatosParaExportar([FromQuery] Guid proveedorId, [FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != proveedorId.ToString() && !User.IsInRole("Admin")) return Forbid();

            var datos = await _citaService.GetCitasRangoAsync(proveedorId, inicio, fin);
            return Ok(datos);
        }

        // --- 📝 5. AGENDAR CITA (CON RETORNO DE TOKEN) ---
        [HttpPost("agendar")]
        [AllowAnonymous] 
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 🛡️ BLINDAJE JWT: Si es un cliente logueado, protegemos que no agende para otro ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && User.IsInRole("Cliente"))
            {
                dto.ClienteId = Guid.Parse(userIdClaim);
            }

            var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            
            return Ok(new { 
                message = result.Message, 
                citaId = result.CitaId,
                modalidad = dto.Modalidad,
                registro = dto.MetodoRegistro ?? "Web"
            });
        }

        // 🛡️ NUEVO: --- 🚩 5.1 VALIDAR CHECK-IN (TOKEN) ---
        [HttpPost("validar-checkin")]
        public async Task<IActionResult> ValidarCheckIn([FromBody] CheckInDto dto)
        {
            if (dto.CitaId == Guid.Empty || string.IsNullOrEmpty(dto.Token))
                return BadRequest(new { message = "Cita ID y Token son obligatorios." });

            // 🚩 Conexión con el método del Service
            var result = await _citaService.ConfirmarAsistenciaAsync(dto.CitaId, dto.Token);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        // --- 🔍 6. CONSULTAR AGENDA POR PROVEEDOR ---
        [HttpGet("agenda/{proveedorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            var fechaConsulta = fecha ?? DateTime.Today;
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fechaConsulta);
            return Ok(agenda);
        }

        // --- 🕒 7. DISPONIBILIDAD (MOTOR OVERBOOKING PRO) ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime? fecha)
        {
            if (!fecha.HasValue || fecha.Value == DateTime.MinValue)
            {
                fecha = DateTime.Today;
            }

            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fecha.Value);
            
            if (slots == null || !slots.Any())
            {
                return Ok(new List<TimeSpan>()); 
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

        // --- 📜 9. HISTORIAL (Blindaje de Privacidad) ---
        [HttpGet("historial/{clienteId}")]
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // 🛡️ Un cliente solo puede ver SU propio historial
            if (User.IsInRole("Cliente") && userIdClaim != clienteId.ToString())
                return Forbid();

            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            if (historial == null || !historial.Any())
                return Ok(new { message = "Este cliente aún no tiene citas en su historial." });
            return Ok(historial);
        }

        // --- 📍 10. UBICACIÓN ---
        [HttpGet("{id}/ubicacion")]
        public async Task<IActionResult> GetUbicacionDomicilio(Guid id)
        {
            // Rango preventivo para búsqueda de metadata de ubicación
            var datos = await _citaService.GetCitasRangoAsync(Guid.Empty, DateTime.Today.AddMonths(-3), DateTime.Today.AddMonths(3));
            var cita = datos.Cast<dynamic>().FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "Cita no encontrada" });
            
            return Ok(new {
                direccion = cita.Direccion,
                modalidad = cita.Modalidad
            });
        }
    }
}