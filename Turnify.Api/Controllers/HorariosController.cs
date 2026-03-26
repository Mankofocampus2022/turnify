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
            public async Task<IActionResult> ConfigurarSemana(List<HorarioAtencionDto> horariosDto)
            {
                var usuarioIdFinal = Guid.Parse("869d1ce6-1161-4ab1-b89c-a067ce0d6ad2"); // Truco Admin

                var proveedor = await _context.proveedores
                    .FirstOrDefaultAsync(p => p.UsuarioId == usuarioIdFinal);

                if (proveedor == null) return BadRequest("Proveedor no encontrado.");

                // Limpieza de seguridad
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
                return Ok(new { mensaje = "Horario de 9am a 8pm configurado correctamente." });
            }

        [HttpGet("mi-semana")]
        public async Task<ActionResult<IEnumerable<HorariosAtencion>>> GetMiSemana()
        {
            // Reutilizamos el truco para ver los horarios del proveedor actual
            var usuarioIdFinal = Guid.Parse("869d1ce6-1161-4ab1-b89c-a067ce0d6ad2");
            var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioIdFinal);

            if (proveedor == null) return NotFound();

            return await _context.horarios_atencion
                .Where(h => h.ProveedorId == proveedor.Id)
                .OrderBy(h => h.DiaSemana)
                .ToListAsync();
        }
    }
}