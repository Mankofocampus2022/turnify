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

        // --- 📅 1. AGENDA DE HOY (Filtro Estricto Bogota Time) ---
        [HttpGet("hoy")]
        public async Task<IActionResult> GetCitasHoy()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            // 🛡️ Blindaje: Guid.TryParse evita excepciones si el token viene corrupto
            if (!Guid.TryParse(userIdClaim, out Guid userId))
                return BadRequest(new { message = "Identificador de usuario malformado." });

            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            return Ok(agenda);
        }

        // --- 📊 2. CITAS POR RANGO (Blindado contra fechas nulas y desbordamiento) ---
        [HttpGet("rango")]
        public async Task<IActionResult> GetCitasRango([FromQuery] DateTime? inicio, [FromQuery] DateTime? fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

            var fechaInicio = inicio ?? DateTime.Today;
            var fechaFin = fin ?? DateTime.Today;

            if (fechaFin < fechaInicio) 
                return BadRequest(new { message = "La fecha final no puede ser anterior a la inicial." });

            if (!Guid.TryParse(userIdClaim, out Guid userId))
                return BadRequest(new { message = "Identidad de usuario no válida." });

            var agenda = await _citaService.GetCitasRangoAsync(userId, fechaInicio, fechaFin);
            return Ok(agenda);
        }

        // 📈 --- 3. ANALÍTICA AVANZADA (Intacta con Seguridad Reforzada) ---
        [HttpGet("analitica-avanzada")]
        public async Task<IActionResult> GetAnaliticaAvanzada([FromQuery] Guid proveedorId, [FromQuery] string periodo = "mes", [FromQuery] DateTime? fecha = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // 🛡️ Blindaje: Solo el dueño de la data o Admin pueden ver analítica sensible
            bool esDuenio = !string.IsNullOrEmpty(userIdClaim) && userIdClaim.Equals(proveedorId.ToString(), StringComparison.OrdinalIgnoreCase);
            if (proveedorId != Guid.Empty && !esDuenio && !User.IsInRole("Admin"))
                return Forbid();

            var analitica = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha, periodo);
            if (analitica == null) return NotFound(new { message = "No hay datos analíticos para este periodo" });
            return Ok(analitica);
        }

        // 📥 --- 4. EXPORTAR DATA (Control de Acceso estricto) ---
        [HttpGet("exportar/datos")]
        public async Task<IActionResult> GetDatosParaExportar([FromQuery] Guid proveedorId, [FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            if (userIdClaim != proveedorId.ToString() && !User.IsInRole("Admin")) return Forbid();

            var datos = await _citaService.GetCitasRangoAsync(proveedorId, inicio, fin);
            return Ok(datos);
        }

        // --- 📝 5. AGENDAR CITA (CON PROTECCIÓN DE IDENTIDAD) ---
        [HttpPost("agendar")]
        [AllowAnonymous] 
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Petición nula." });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 🛡️ BLINDAJE JWT: Mapeo inteligente de ClienteID vs UsuarioID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var clienteIdClaim = User.FindFirst("ClienteId")?.Value; // 🚩 Nueva claim inyectada en el login

            if (!string.IsNullOrEmpty(userIdClaim) && User.IsInRole("Cliente"))
            {
                // 🚩 FIX MAESTRO: Priorizamos el ID de la tabla Clientes si está en el token
                if (!string.IsNullOrEmpty(clienteIdClaim) && Guid.TryParse(clienteIdClaim, out Guid realClientId))
                {
                    dto.ClienteId = realClientId;
                    Console.WriteLine($"🔍 [Turnify Auth] Usando ClienteID real: {realClientId}");
                }
                else if (Guid.TryParse(userIdClaim, out Guid authUserId))
                {
                    // Si no está el ClienteID, usamos el UsuarioID (pero esto puede fallar si el service no lo mapea)
                    dto.ClienteId = authUserId;
                    Console.WriteLine($"⚠️ [Turnify Auth] Advertencia: Usando UsuarioID como ClienteID: {authUserId}");
                }
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

        // 🛡️ --- 🚩 5.1 VALIDAR CHECK-IN (TOKEN) ---
        [HttpPost("validar-checkin")]
        public async Task<IActionResult> ValidarCheckIn([FromBody] CheckInDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de check-in requeridos." });
            if (dto.CitaId == Guid.Empty || string.IsNullOrEmpty(dto.Token))
                return BadRequest(new { message = "Cita ID y Token son obligatorios para el Check-in." });

            var result = await _citaService.ConfirmarAsistenciaAsync(dto.CitaId, dto.Token);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        // --- 🔍 6. CONSULTAR AGENDA POR PROVEEDOR (Para Front Público) ---
        [HttpGet("agenda/{proveedorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            if (proveedorId == Guid.Empty) return BadRequest(new { message = "ID de proveedor inválido." });
            var fechaConsulta = fecha ?? DateTime.Today;
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fechaConsulta);
            return Ok(agenda);
        }

        // --- 🕒 7. DISPONIBILIDAD (MOTOR OVERBOOKING PRO CON VALIDACIÓN DINÁMICA) ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime? fecha)
        {
            if (proveedorId == Guid.Empty || servicioId == Guid.Empty)
                return BadRequest(new { message = "Proveedor y Servicio son requeridos para calcular el túnel de tiempo." });

            var fechaConsulta = fecha ?? DateTime.Today;
            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fechaConsulta);
            
            if (slots == null || !slots.Any())
            {
                return Ok(new List<TimeSpan>()); 
            }

            return Ok(slots);
        }

        // --- ⚡ 8. ACTUALIZAR ESTADO (Auditado) ---
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> UpdateEstado(Guid id, [FromBody] EstadoUpdateDto dto)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita no válido." });
            if (dto == null || string.IsNullOrEmpty(dto.NuevoEstado))
                return BadRequest(new { message = "El nuevo estado es requerido." });

            var result = await _citaService.UpdateEstadoCitaAsync(id, dto.NuevoEstado);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            
            return Ok(new { message = result.Message });
        }

        // --- 📜 9. HISTORIAL (Blindaje Habeas Data) ---
        [HttpGet("historial/{clienteId}")]
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            if (clienteId == Guid.Empty) return BadRequest(new { message = "Cliente no identificado." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var clienteIdClaim = User.FindFirst("ClienteId")?.Value;
            
            // 🛡️ Seguridad: Un cliente NO puede husmear el historial de otros IDs
            // Validamos contra ambos IDs por si el frontend manda el del usuario o el del cliente
            if (User.IsInRole("Cliente"))
            {
                bool esPropio = userIdClaim == clienteId.ToString() || clienteIdClaim == clienteId.ToString();
                if (!esPropio) return Forbid();
            }

            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            if (historial == null || !historial.Any())
                return Ok(new { message = "Sin registros previos para este perfil." });
            
            return Ok(historial);
        }

        // --- 📍 10. UBICACIÓN (Blindada y optimizada) ---
        [HttpGet("{id}/ubicacion")]
        public async Task<IActionResult> GetUbicacionDomicilio(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita requerido." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            // 🛡️ Refactor: Solo buscamos en la agenda del profesional logueado por seguridad
            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            var cita = agenda.FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "Cita no encontrada o acceso denegado." });
            
            // 🚩 Para que compile: Asegúrate que CitaResponseDto tenga estas propiedades
            return Ok(new {
                direccion = cita.Direccion,
                modalidad = cita.Modalidad,
                token = cita.CodigoVerificacion
            });
        }

        // --- 💎 11. [NUEVO] DETALLE DE CITA (Blindado) ---
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetalleCita(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita requerido." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            var cita = agenda.FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "No se pudo recuperar la información de la cita." });
            return Ok(cita);
        }
    }
}