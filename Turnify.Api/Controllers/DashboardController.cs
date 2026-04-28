using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Data; 
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
        private readonly TurnifyDbContext _context; 

        public DashboardController(IDashboardService dashboardService, TurnifyDbContext context)
        {
            _dashboardService = dashboardService;
            _context = context;
        }

        // 🚩 ENDPOINT PRINCIPAL: Soporte para periodos (diario/semana/mes)
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen([FromQuery] string periodo = "diario", [FromQuery] DateTime? fecha = null)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (usuarioIdClaim == null) return Unauthorized();

            var proveedor = await _context.proveedores
                .FirstOrDefaultAsync(p => p.UsuarioId == Guid.Parse(usuarioIdClaim));

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de negocio para este usuario." });
            }

            object resumen;
            // 🛡️ REPARACIÓN SENIOR: 
            // Si el periodo es mensual, usamos el método específico o el genérico según tu arquitectura
            if (periodo.ToLower() == "mensual")
            {
                resumen = await _dashboardService.GetResumenMensualAsync(proveedor.Id);
            }
            else
            {
                // 🚩 CAMBIO CLAVE: Pasamos el 'periodo' (hoy/semana/mes) al Service 
                // para que el filtro de fechas en SQL no se limite a 24 horas.
                resumen = await _dashboardService.GetResumenDiarioAsync(proveedor.Id, fecha, periodo);
            }

            if (resumen == null)
            {
                return NotFound("No se encontraron datos para este proveedor.");
            }

            return Ok(resumen);
        }

        // 🚩 VERSIÓN ADMIN: Consultar cualquier proveedor con filtros
        [HttpGet("resumen/{proveedorId}")]
        public async Task<IActionResult> GetResumenPorId(Guid proveedorId, [FromQuery] string periodo = "diario", [FromQuery] DateTime? fecha = null)
        {
            if (proveedorId == Guid.Empty) return BadRequest("El ID del proveedor no es válido.");

            // 🛡️ PUENTE DE IDENTIDAD: Sincronización de IDs (67F6... vs 93E4...)
            var proveedorEncontrado = await _context.proveedores
                .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);

            var idRealParaServicio = proveedorEncontrado != null ? proveedorEncontrado.Id : proveedorId;

            object resumen;
            if (periodo.ToLower() == "mensual")
            {
                resumen = await _dashboardService.GetResumenMensualAsync(idRealParaServicio);
            }
            else
            {
                // 🚩 CAMBIO CLAVE: Pasamos el 'periodo' para que el Service 
                // sepa que si mandas 'mes', debe buscar 30 días y no solo 1.
                resumen = await _dashboardService.GetResumenDiarioAsync(idRealParaServicio, fecha, periodo);
            }

            if (resumen == null) return NotFound("No hay datos para este periodo.");

            return Ok(resumen);
        }
    }
}