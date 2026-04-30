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
        public async Task<IActionResult> GetResumen(
            [FromQuery] string periodo = "diario", 
            [FromQuery] DateTime? fecha = null,
            [FromQuery] int? mes = null,    
            [FromQuery] int? anio = null)   
        {
            // 🛡️ CORRECCIÓN CRÍTICA: Usamos NameIdentifier para rescatar el ID del token
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (usuarioIdClaim == null) return Unauthorized(new { message = "Sesión no válida" });

            // 🛡️ RESCATE DE IDENTIDAD: Buscamos el perfil de proveedor amarrado al usuario
            var proveedor = await _context.proveedores
                .FirstOrDefaultAsync(p => p.UsuarioId == Guid.Parse(usuarioIdClaim));

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de negocio para este usuario." });
            }

            object resumen;
            
            // 🛡️ REPARACIÓN SENIOR: 
            // Si vienen mes y año, priorizamos el GetResumenDiarioAsync con esos filtros para evitar el bug de fechas
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(proveedor.Id);
            }
            else
            {
                // 🚩 Pasamos mes y anio para que el Service limpie los datos de otros meses
                resumen = await _dashboardService.GetResumenDiarioAsync(proveedor.Id, fecha, periodo, mes, anio);
            }

            if (resumen == null)
            {
                return NotFound("No se encontraron datos para este proveedor.");
            }

            return Ok(resumen);
        }

        // 🚩 VERSIÓN ADMIN: Consultar cualquier proveedor con filtros
        [HttpGet("resumen/{proveedorId}")]
        public async Task<IActionResult> GetResumenPorId(
            Guid proveedorId, 
            [FromQuery] string periodo = "diario", 
            [FromQuery] DateTime? fecha = null,
            [FromQuery] int? mes = null,    
            [FromQuery] int? anio = null)   
        {
            if (proveedorId == Guid.Empty) return BadRequest("El ID del proveedor no es válido.");

            // 🛡️ PUENTE DE IDENTIDAD: Sincronización de IDs
            var proveedorEncontrado = await _context.proveedores
                .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);

            var idRealParaServicio = proveedorEncontrado != null ? proveedorEncontrado.Id : proveedorId;

            object resumen;
            
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(idRealParaServicio);
            }
            else
            {
                // 🚩 Ahora el servicio recibe mes y año. Esto mata el bug de ver Abril en Junio.
                resumen = await _dashboardService.GetResumenDiarioAsync(idRealParaServicio, fecha, periodo, mes, anio);
            }

            if (resumen == null) return NotFound("No hay datos para este periodo.");

            return Ok(resumen);
        }
    }
}