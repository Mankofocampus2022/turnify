using Microsoft.AspNetCore.Authorization; // Necesario para [Authorize]
using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Interfaces;
using Turnify.Api.Dtos; // Asumiendo que crearás una carpeta Dtos

namespace Turnify.Api.Controllers
{
    [Authorize] // 🛡️ BLOQUEO: Solo usuarios con token pueden consultar estadísticas
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("resumen/{proveedorId}")]
        public async Task<IActionResult> GetResumen(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            // Validamos que el ID sea válido
            if (proveedorId == Guid.Empty)
            {
                return BadRequest("El ID del proveedor no es válido.");
            }

            // Llamamos al servicio (la lógica pesada)
            // Si la fecha viene nula, el servicio usará DateTime.Today por defecto
            var resumen = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha);

            if (resumen == null)
            {
                return NotFound("No se encontraron datos para este proveedor.");
            }

            return Ok(resumen);
        }
    }
}