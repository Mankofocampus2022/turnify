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

        // GUID constante de Rol Staff/Empleado
        private static readonly Guid ROL_STAFF_ID = Guid.Parse("99A2B3C4-E5F6-4789-90AB-C1D2E3F40099");

        public DashboardController(IDashboardService dashboardService, TurnifyDbContext context)
        {
            _dashboardService = dashboardService;
            _context = context;
        }

        // 🚩 MÉTODO PRIVADO: Sincronización horaria estricta de Bogotá para capas analíticas en Docker (INTACTO)
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

        // 🌐 NUEVO MÉTODO GLOBAL: Lee el país del usuario y usa Bogotá como red de seguridad
        private DateTime GetLocalToday()
        {
            var timeZoneHeader = Request.Headers["X-TimeZone"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(timeZoneHeader))
            {
                try 
                {
                    var localZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneHeader);
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localZone).Date;
                }
                catch 
                {
                    // Si el frontend envía una zona inválida, ignora y sigue abajo
                }
            }

            // Fallback a tu lógica original intacta
            return GetBogotaToday();
        }

        // 🚩 ENDPOINT PRINCIPAL: Soporte para periodos (diario/semana/mes) y Módulo Staff
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen(
            [FromQuery] string periodo = "diario", 
            [FromQuery] DateTime? fecha = null,
            [FromQuery] int? mes = null,    
            [FromQuery] int? anio = null)   
        {
            // 🛡️ CORRECCIÓN CRÍTICA: Usamos NameIdentifier para rescatar el ID del token
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(usuarioIdClaim) || !Guid.TryParse(usuarioIdClaim, out var userId)) 
                return Unauthorized(new { message = "Sesión no válida o token corrupto." });

            // 🚀 HU 001 - MULTI-SILLA: 1. Identificar si el usuario es STAFF (Empleado)
            var usuario = await _context.usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.id == userId);
            
            if (usuario != null && usuario.rol_id == ROL_STAFF_ID)
            {
                var empleado = await _context.empleados.AsNoTracking().FirstOrDefaultAsync(e => e.UsuarioId == userId);
                if (empleado == null) return NotFound(new { message = "Perfil de empleado no configurado." });

                // Delegamos la liquidación al nuevo cerebro financiero usando la fecha global
                var resumenEmpleado = await _dashboardService.GetLiquidacionStaffAsync(empleado.Id, fecha ?? GetLocalToday(), periodo, mes, anio);
                return Ok(resumenEmpleado);
            }

            // 🛡️ RESCATE DE IDENTIDAD ORIGINAL: Buscamos el perfil de proveedor amarrado al usuario
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de negocio para este usuario." });
            }

            object resumen;
            
            // Si vienen mes y año, priorizamos el GetResumenDiarioAsync con esos filtros para evitar el bug de fechas
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(proveedor.Id);
            }
            else
            {
                // 🚩 FIX BUG 01/05: Reemplazamos DateTime.Today por la fecha globalizada
                resumen = await _dashboardService.GetResumenDiarioAsync(proveedor.Id, fecha ?? GetLocalToday(), periodo, mes, anio);
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
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);

            var idRealParaServicio = proveedorEncontrado != null ? proveedorEncontrado.Id : proveedorId;

            object resumen;
            
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(idRealParaServicio);
            }
            else
            {
                // 🚩 FIX BUG ADMIN: Sincronización horaria globalizada
                resumen = await _dashboardService.GetResumenDiarioAsync(idRealParaServicio, fecha ?? GetLocalToday(), periodo, mes, anio);
            }

            if (resumen == null) return NotFound(new { message = "No hay datos para este periodo." });

            return Ok(resumen);
        }

        // =========================================================================
        // 💈 HU-06 & HU-07: MÓDULO EXCLUSIVO PARA PROFESIONAL INDEPENDIENTE
        // =========================================================================

        /// <summary>
        /// Obtiene el resumen del panel de control diario para un profesional independiente (HU-06 y HU-07).
        /// Aislamiento total de datos mediante JWT y cálculo de ingresos 100% brutos sin deducción de comisión.
        /// </summary>
        [HttpGet("independiente")]
        public async Task<IActionResult> GetDashboardIndependiente(
            [FromQuery] DateTime? fecha = null,
            [FromQuery] string periodo = "diario",
            [FromQuery] int? mes = null,
            [FromQuery] int? anio = null)
        {
            // 🛡️ HU-06 (CA4) AISLAMIENTO DE DATOS: Extraer y validar el Guid del claims
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(usuarioIdClaim) || !Guid.TryParse(usuarioIdClaim, out var userId)) 
                return Unauthorized(new { message = "Sesión no válida o no autenticada." });

            // Rescate de identidad del Proveedor con AsNoTracking para optimizar lectura
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de profesional independiente configurado para esta cuenta." });
            }

            // Aplicar la sincronización horaria global con la cabecera X-TimeZone
            DateTime fechaFiltro = fecha ?? GetLocalToday();

            // Consumo de la capa de servicio para Independientes
            var resumenIndependiente = await _dashboardService.GetDashboardIndependienteAsync(
                proveedor.Id, 
                fechaFiltro, 
                periodo, 
                mes, 
                anio
            );

            if (resumenIndependiente == null)
            {
                return NotFound(new { message = "No se encontraron registros de agenda o métricas para este profesional." });
            }

            return Ok(resumenIndependiente);
        }
    }
}