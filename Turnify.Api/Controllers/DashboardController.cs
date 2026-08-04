using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Data; 
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Turnify.Api.Services.Strategies; // 👈 Import de la estrategia financiera

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
        [HttpGet("ResumenIndependiente")]
        [HttpGet("resumen-independiente")]
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

        // =========================================================================
        // 🚀 HU-20 & HU-21: DETALLE DE MOVIMIENTOS Y LIQUIDACIÓN (PATRÓN STRATEGY)
        // =========================================================================

        /// <summary>
        /// Retorna el detalle financiero de movimientos aplicando el Patrón Strategy.
        /// Si el proveedor es independiente (HU-21), aplica retención 100%.
        /// Si el proveedor es dependiente/multi-silla (HU-20), calcula deducción por comisión de especialista.
        /// </summary>
        [HttpGet("movimientos")]
        [HttpGet("detalle-movimientos")]
        public async Task<IActionResult> GetDetalleMovimientos(
            [FromQuery] DateTime? fecha = null,
            [FromQuery] string periodo = "diario",
            [FromQuery] int? mes = null,
            [FromQuery] int? anio = null)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim) || !Guid.TryParse(usuarioIdClaim, out var userId))
                return Unauthorized(new { message = "Sesión no válida o token corrupto." });

            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor == null)
                return NotFound(new { message = "Perfil de negocio no encontrado." });

            // 1. Determinar el tipo de modelo de negocio (Flag EsIndependiente)
            bool esIndependiente = proveedor.EsIndependiente;

            // 2. Instanciar dinámicamente la estrategia mediante el Factory
            ILiquidacionStrategy strategy = LiquidacionStrategyFactory.ObtenerEstrategia(esIndependiente);

            // 3. Obtener el rango de fechas en DateTimeOffset para ser compatible con Citas.Fecha
            DateTime fechaBase = fecha ?? GetLocalToday();
            DateTimeOffset fechaInicio = new DateTimeOffset(fechaBase.Date);
            DateTimeOffset fechaFin = new DateTimeOffset(fechaBase.Date.AddDays(1).AddTicks(-1));

            if (periodo.ToLower() == "mes" || periodo.ToLower() == "mensual")
            {
                int targetMes = mes ?? fechaBase.Month;
                int targetAnio = anio ?? fechaBase.Year;
                DateTime inicioMes = new DateTime(targetAnio, targetMes, 1);
                fechaInicio = new DateTimeOffset(inicioMes);
                fechaFin = new DateTimeOffset(inicioMes.AddMonths(1).AddTicks(-1));
            }

            // 4. CONSULTA EN BASE DE DATOS: Cargar explícitamente en memoria con ToListAsync()
            var citas = await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Empleado)
                .Where(c => c.ProveedorId == proveedor.Id 
                       && c.Fecha >= fechaInicio 
                       && c.Fecha <= fechaFin)
                .ToListAsync();

            // 5. MAPEO EN MEMORIA (LINQ to Objects): Mapeo defensivo libre de errores de traducción SQL
            var detalleMovimientos = citas.Select(c => 
            {
                // Extraer Nombre del Cliente de forma resiliente
                string clienteNom = "Cliente General";
                if (c.Cliente != null)
                {
                    var propNombre = c.Cliente.GetType().GetProperty("Nombre") ?? c.Cliente.GetType().GetProperty("nombre");
                    var propApellido = c.Cliente.GetType().GetProperty("Apellido") ?? c.Cliente.GetType().GetProperty("apellido");

                    var valNombre = propNombre?.GetValue(c.Cliente)?.ToString();
                    var valApellido = propApellido?.GetValue(c.Cliente)?.ToString();

                    clienteNom = $"{valNombre} {valApellido}".Trim();
                    if (string.IsNullOrWhiteSpace(clienteNom)) clienteNom = "Cliente General";
                }

                // Extraer Nombre del Servicio
                string servicioNom = "Servicio General";
                if (c.Servicio != null)
                {
                    var propServ = c.Servicio.GetType().GetProperty("Nombre") ?? c.Servicio.GetType().GetProperty("nombre");
                    servicioNom = propServ?.GetValue(c.Servicio)?.ToString() ?? "Servicio General";
                }

                // 🚀 HOTFIX ESPECIALISTA: Fallback resiliente que evita desplegar "No Asignado"
                string especialistaNom = string.Empty;
                decimal comision = 0m;

                if (c.Empleado != null)
                {
                    var propEmpNom = c.Empleado.GetType().GetProperty("Nombre") ?? c.Empleado.GetType().GetProperty("nombre");
                    var propEmpApe = c.Empleado.GetType().GetProperty("Apellido") ?? c.Empleado.GetType().GetProperty("apellido");
                    
                    var empN = propEmpNom?.GetValue(c.Empleado)?.ToString();
                    var empA = propEmpApe?.GetValue(c.Empleado)?.ToString();

                    especialistaNom = !string.IsNullOrEmpty(empA) ? $"{empN} {empA}".Trim() : (empN ?? string.Empty);

                    var propCom = c.Empleado.GetType().GetProperty("PorcentajeComision") 
                               ?? c.Empleado.GetType().GetProperty("ComisionPorcentaje")
                               ?? c.Empleado.GetType().GetProperty("porcentaje_comision")
                               ?? c.Empleado.GetType().GetProperty("comision");

                    if (propCom != null)
                    {
                        var valCom = propCom.GetValue(c.Empleado);
                        if (valCom != null && decimal.TryParse(valCom.ToString(), out var parsedCom))
                            comision = parsedCom;
                    }
                }

                // Si el empleado asignado no tiene un nombre configurado o es null (ej. atención directa o independiente)
                if (string.IsNullOrWhiteSpace(especialistaNom))
                {
                    // Usa el NombreComercial o Nombre del proveedor/negocio
                    var propProvNom = proveedor.GetType().GetProperty("NombreComercial") ?? proveedor.GetType().GetProperty("Nombre") ?? proveedor.GetType().GetProperty("nombre");
                    especialistaNom = propProvNom?.GetValue(proveedor)?.ToString() ?? "Especialista Asignado";
                }

                decimal montoTotal = c.PrecioPactado + c.CostoDomicilio;

                return strategy.CalcularMovimiento(
                    citaId: c.Id,
                    fecha: c.Fecha.DateTime,
                    clienteNombre: clienteNom,
                    servicioNombre: servicioNom,
                    montoTotal: montoTotal,
                    porcentajeComision: comision,
                    estado: c.Estado ?? "Completada",
                    especialistaNombre: especialistaNom
                );
            }).ToList();

            // 6. Respuesta limpia serializada correctamente
            int totalRegistros = detalleMovimientos.Count;

            return Ok(new
            {
                TipoModelo = esIndependiente ? "Independiente" : "Dependiente",
                TotalMovimientos = totalRegistros,
                MontoTotalAcumulado = detalleMovimientos.Sum(m => m.MontoTotal),
                IngresoNetoTotal = detalleMovimientos.Sum(m => m.IngresoNeto),
                ComisionesTotalesPagadas = detalleMovimientos.Sum(m => m.MontoComisionEspecialista),
                Movimientos = detalleMovimientos
            });
        }
    }
}