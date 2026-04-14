using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs; // Sincronizado con tus otros DTOs
using Turnify.Api.Data; // Para buscar el proveedor vinculado
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Turnify.Api.Controllers
{
    [Authorize] 
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly TurnifyDbContext _context; // Agregado para resolver el ProveedorId

        public DashboardController(IDashboardService dashboardService, TurnifyDbContext context)
        {
            _dashboardService = dashboardService;
            _context = context;
        }

        // Endpoint original (Lo mantenemos como pediste, pero lo hacemos dinámico)
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] DateTime? fecha)
        {
            // 1. Extraemos el ID del usuario directamente del Token (Identidad blindada)
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (usuarioIdClaim == null) return Unauthorized();

            // 2. Buscamos el proveedor que le pertenece a este usuario
            var proveedor = await _context.proveedores
                .FirstOrDefaultAsync(p => p.UsuarioId == Guid.Parse(usuarioIdClaim));

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de negocio para este usuario." });
            }

            // 3. Llamamos al servicio con el ID real validado por el servidor
            var resumen = await _dashboardService.GetResumenDiarioAsync(proveedor.Id, fecha);

            if (resumen == null)
            {
                return NotFound("No se encontraron datos para este proveedor.");
            }

            return Ok(resumen);
        }

        // 🚩 Mantenemos la versión con ID por si necesitas consultar desde un panel de SuperAdmin
        [HttpGet("resumen/{proveedorId}")]
        public async Task<IActionResult> GetResumenPorId(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            if (proveedorId == Guid.Empty) return BadRequest("El ID del proveedor no es válido.");

            var resumen = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha);
            return Ok(resumen);
        }
    }
}