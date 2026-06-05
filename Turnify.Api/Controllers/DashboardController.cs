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

        // 🚩 MÉTODO PRIVADO: Sincronización horaria estricta de Bogotá para capas analíticas en Docker
        private DateTime GetBogotaToday()
        {
            try 
            {
                var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone).Date;
            }
            catch 
            {
                // Fallback manual UTC-5 libre de excepciones geográficas
                return DateTime.UtcNow.AddHours(-5).Date;
            }
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
            
            if (string.IsNullOrEmpty(usuarioIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

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
                // 🚩 FIX BUG 01/05: Reemplazamos DateTime.Today por la fecha normalizada de Bogotá para amarrar la agregación diaria
                resumen = await _dashboardService.GetResumenDiarioAsync(proveedor.Id, fecha ?? GetBogotaToday(), periodo, mes, anio);
            }

            if (resumen == null)
            {
                return NotFound(new { message = "No se encontraron datos para este proveedor." });
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
            if (proveedorId == Guid.Empty) return BadRequest(new { message = "El ID del proveedor no es válido." });

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
                // 🚩 FIX BUG ADMIN: Sincronización horaria estricta de Bogotá para consultas delegadas de analítica
                resumen = await _dashboardService.GetResumenDiarioAsync(idRealParaServicio, fecha ?? GetBogotaToday(), periodo, mes, anio);
            }

            if (resumen == null) return NotFound(new { message = "No hay datos para este periodo." });

            return Ok(resumen);
        }
    }
}