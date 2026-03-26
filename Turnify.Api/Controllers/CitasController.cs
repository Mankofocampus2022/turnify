using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CitasController : ControllerBase
    {
        private readonly ICitaService _citaService;
        public CitasController(ICitaService citaService) => _citaService = citaService;

        [HttpPost("agendar")]
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message, citaId = result.CitaId });
        }

        [HttpGet("agenda/{proveedorId}")]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime fecha)
        {
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fecha);
            return Ok(agenda);
        }

        [HttpGet("disponibilidad")]
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime fecha)
        {
            var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fecha);
            return Ok(slots);
        }

        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> UpdateEstado(Guid id, [FromQuery] string nuevoEstado)
        {
            var result = await _citaService.UpdateEstadoCitaAsync(id, nuevoEstado);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message });
        }

        // Nuevo endpoint para obtener el historial de citas de un cliente
        // GET: api/Citas/historial/{clienteId}
        [HttpGet("historial/{clienteId}")]
        [Authorize] // 🛡️ Solo usuarios con Token pueden ver esto
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            
            // Si la lista está vacía, devolvemos un mensaje amigable
            if (historial == null || !historial.Any())
                return Ok(new { message = "Este cliente aún no tiene citas en su historial." });

            return Ok(historial);
        }

    }
}