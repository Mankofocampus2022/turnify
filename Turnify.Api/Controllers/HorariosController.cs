using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using System.Security.Claims;

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HorariosController : ControllerBase
    {
        private readonly TurnifyDbContext _context;

        public HorariosController(TurnifyDbContext context)
        {
            _context = context;
        }

        [HttpPost("configurar-semana")]
        public async Task<IActionResult> ConfigurarSemana([FromBody] List<HorarioAtencionDto> horariosDto)
        {
            var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (usuarioId == null) return Unauthorized();

            var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == Guid.Parse(usuarioId));
            if (proveedor == null) return BadRequest("Proveedor no encontrado.");

            var horariosViejos = _context.horarios_atencion.Where(h => h.ProveedorId == proveedor.Id);
            _context.horarios_atencion.RemoveRange(horariosViejos);

            foreach (var dto in horariosDto)
            {
                _context.horarios_atencion.Add(new HorariosAtencion
                {
                    Id = Guid.NewGuid(),
                    ProveedorId = proveedor.Id,
                    DiaSemana = dto.DiaSemana,
                    HoraApertura = TimeSpan.Parse(dto.HoraApertura),
                    HoraCierre = TimeSpan.Parse(dto.HoraCierre)
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "✅ Horario semanal actualizado con éxito." });
        }

     [HttpGet("mi-semana")]
            public async Task<IActionResult> GetMiSemana()
            {
                // 1. Obtenemos el ID del Token
                var usuarioIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (usuarioIdClaim == null) return Unauthorized();

                // 2. Buscamos el proveedor
                var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == Guid.Parse(usuarioIdClaim));

                if (proveedor == null) return NotFound(new { mensaje = "Proveedor no encontrado" });

                // 3. 🚩 LA MAGIA: Proyectamos a un objeto anónimo con las horas como String
                var horarios = await _context.horarios_atencion
                    .Where(h => h.ProveedorId == proveedor.Id)
                    .OrderBy(h => h.DiaSemana)
                    .Select(h => new {
                        h.Id,
                        h.ProveedorId,
                        h.DiaSemana,
                        // Convertimos TimeSpan a formato "08:00"
                        HoraApertura = h.HoraApertura.ToString(@"hh\:mm"),
                        HoraCierre = h.HoraCierre.ToString(@"hh\:mm")
                    })
                    .ToListAsync();

                return Ok(horarios);
        }
    }
}