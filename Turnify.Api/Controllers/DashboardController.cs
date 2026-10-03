using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Data; 
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Turnify.Api.Services.Strategies;

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
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(usuarioIdClaim) || !Guid.TryParse(usuarioIdClaim, out var userId)) 
                return Unauthorized(new { message = "Sesión no válida o token corrupto." });

            var usuario = await _context.usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.id == userId);
            
            if (usuario != null && usuario.rol_id == ROL_STAFF_ID)
            {
                var empleado = await _context.empleados.AsNoTracking().FirstOrDefaultAsync(e => e.UsuarioId == userId);
                if (empleado == null) return NotFound(new { message = "Perfil de empleado no configurado." });

                var resumenEmpleado = await _dashboardService.GetLiquidacionStaffAsync(empleado.Id, fecha ?? GetLocalToday(), periodo, mes, anio);
                return Ok(resumenEmpleado);
            }

            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de negocio para este usuario." });
            }

            object? resumen;
            
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(proveedor.Id);
            }
            else
            {
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

            var proveedorEncontrado = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);

            var idRealParaServicio = proveedorEncontrado != null ? proveedorEncontrado.Id : proveedorId;

            object? resumen;
            
            if ((periodo.ToLower() == "mensual" || periodo.ToLower() == "mes") && !mes.HasValue)
            {
                resumen = await _dashboardService.GetResumenMensualAsync(idRealParaServicio);
            }
            else
            {
                resumen = await _dashboardService.GetResumenDiarioAsync(idRealParaServicio, fecha ?? GetLocalToday(), periodo, mes, anio);
            }

            if (resumen == null) return NotFound(new { message = "No hay datos para este periodo." });

            return Ok(resumen);
        }

        // =========================================================================
        // 💈 HU-06 & HU-07: MÓDULO EXCLUSIVO PARA PROFESIONAL INDEPENDIENTE
        // =========================================================================

        [HttpGet("independiente")]
        [HttpGet("ResumenIndependiente")]
        [HttpGet("resumen-independiente")]
        public async Task<IActionResult> GetDashboardIndependiente(
            [FromQuery] DateTime? fecha = null,
            [FromQuery] string periodo = "diario",
            [FromQuery] int? mes = null,
            [FromQuery] int? anio = null)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(usuarioIdClaim) || !Guid.TryParse(usuarioIdClaim, out var userId)) 
                return Unauthorized(new { message = "Sesión no válida o no autenticada." });

            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor == null)
            {
                return NotFound(new { message = "No se encontró un perfil de profesional independiente configurado para esta cuenta." });
            }

            DateTime fechaFiltro = fecha ?? GetLocalToday();

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

            bool esIndependiente = proveedor.EsIndependiente;
            ILiquidacionStrategy strategy = LiquidacionStrategyFactory.ObtenerEstrategia(esIndependiente);

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

            var citas = await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Empleado)
                .Where(c => c.ProveedorId == proveedor.Id 
                       && c.Fecha >= fechaInicio 
                       && c.Fecha <= fechaFin)
                .ToListAsync();

            var detalleMovimientos = citas.Select(c => 
            {
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

                string servicioNom = "Servicio General";
                if (c.Servicio != null)
                {
                    var propServ = c.Servicio.GetType().GetProperty("Nombre") ?? c.Servicio.GetType().GetProperty("nombre");
                    servicioNom = propServ?.GetValue(c.Servicio)?.ToString() ?? "Servicio General";
                }

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

                if (string.IsNullOrWhiteSpace(especialistaNom))
                {
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

        // =========================================================================
        // 🚀 HU-REP01: REPORTES DE CARTERA Y ESTADOS DE SUSCRIPCIÓN (RESILIENTE)
        // =========================================================================

        /// <summary>
        /// Genera el reporte consolidado de cartera y estados de suscripción (HU-REP01).
        /// Manejo defensivo con try-catch para prevenir errores 500 por mapeo de EF Core.
        /// </summary>
        [HttpGet("reportes/cartera")]
        [HttpGet("reportes/suscripciones")]
        [HttpGet("cartera")]
        public async Task<IActionResult> GetReporteCartera(
            [FromQuery] string estado = "todos",
            [FromQuery] string busqueda = "")
        {
            try
            {
                var hoy = GetLocalToday();

                // Cargar proveedores de forma resiliente
                List<object> proveedoresLista = new List<object>();

                try 
                {
                    var provsConInclude = await _context.proveedores
                        .AsNoTracking()
                        .Include(p => p.Suscripciones)
                        .ToListAsync();

                    proveedoresLista = provsConInclude.Cast<object>().ToList();
                }
                catch 
                {
                    // Fallback directo si no existe la propiedad de navegación explicita en EF Core
                    var provsSinInclude = await _context.proveedores
                        .AsNoTracking()
                        .ToListAsync();

                    proveedoresLista = provsSinInclude.Cast<object>().ToList();
                }

                var reporteItems = proveedoresLista.Select(p => 
                {
                    var tP = p.GetType();
                    var provId = (Guid)(tP.GetProperty("Id")?.GetValue(p) ?? Guid.Empty);

                    bool esIndependiente = false;
                    var propEsInd = tP.GetProperty("EsIndependiente") ?? tP.GetProperty("es_independiente");
                    if (propEsInd != null && bool.TryParse(propEsInd.GetValue(p)?.ToString(), out var bInd))
                    {
                        esIndependiente = bInd;
                    }

                    // Extraer colección de suscripciones dinámicamente si existe
                    IEnumerable<object>? suscripcionesCol = null;
                    var propSusc = tP.GetProperty("Suscripciones") ?? tP.GetProperty("suscripciones");
                    if (propSusc != null)
                    {
                        suscripcionesCol = propSusc.GetValue(p) as IEnumerable<object>;
                    }

                    object? ultSuscripcion = null;
                    if (suscripcionesCol != null && suscripcionesCol.Any())
                    {
                        ultSuscripcion = suscripcionesCol
                            .OrderByDescending(s => GetSuscripcionFechaFin(s, DateTime.MinValue))
                            .FirstOrDefault();
                    }

                    DateTime fechaFin = GetSuscripcionFechaFin(ultSuscripcion, hoy.AddDays(30));
                    DateTime fechaInicio = GetSuscripcionFechaInicio(ultSuscripcion, hoy);
                    int diasRestantes = (fechaFin.Date - hoy.Date).Days;
                    bool estadoSuscripcionBool = GetSuscripcionEstado(ultSuscripcion);

                    string estadoCalculado = "Activa";
                    if (ultSuscripcion != null && !estadoSuscripcionBool)
                    {
                        estadoCalculado = "Cancelada";
                    }
                    else if (diasRestantes < 0)
                    {
                        estadoCalculado = "Vencida";
                    }
                    else if (diasRestantes <= 7)
                    {
                        estadoCalculado = "Próximo a Vencer";
                    }

                    decimal montoPagado = GetSuscripcionMonto(ultSuscripcion);
                    string plan = GetSuscripcionPlan(ultSuscripcion, esIndependiente);
                    string clienteNombre = GetProveedorNombre(p);

                    string? emailVal = tP.GetProperty("Email")?.GetValue(p)?.ToString() ?? tP.GetProperty("email")?.GetValue(p)?.ToString();
                    string? telVal = tP.GetProperty("Telefono")?.GetValue(p)?.ToString() ?? tP.GetProperty("telefono")?.GetValue(p)?.ToString();

                    string email = string.IsNullOrWhiteSpace(emailVal) ? "Sin correo registrado" : emailVal;
                    string telefono = string.IsNullOrWhiteSpace(telVal) ? "No registrado" : telVal;

                    return new
                    {
                        ProveedorId = provId,
                        ClienteNombre = clienteNombre,
                        Email = email,
                        Telefono = telefono,
                        PlanAdquirido = plan,
                        MontoPagado = montoPagado,
                        FechaInicio = fechaInicio.ToString("yyyy-MM-dd"),
                        FechaVencimiento = fechaFin.ToString("yyyy-MM-dd"),
                        DiasRestantes = diasRestantes,
                        Estado = estadoCalculado
                    };
                }).ToList();

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    string term = busqueda.Trim().ToLower();
                    reporteItems = reporteItems.Where(i => 
                        (i.ClienteNombre != null && i.ClienteNombre.ToLower().Contains(term)) ||
                        (i.Email != null && i.Email.ToLower().Contains(term)) ||
                        (i.Telefono != null && i.Telefono.ToLower().Contains(term))
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(estado) && estado.ToLower() != "todos")
                {
                    string estFiltro = estado.Trim().ToLower();
                    reporteItems = reporteItems.Where(i => i.Estado.ToLower().Replace(" ", "") == estFiltro.Replace(" ", "") ||
                                                           i.Estado.ToLower().Contains(estFiltro)).ToList();
                }

                var totalSuscriptores = reporteItems.Count;
                var activasCount = reporteItems.Count(i => i.Estado == "Activa");
                var proximosVencerCount = reporteItems.Count(i => i.Estado == "Próximo a Vencer");
                var vencidasCount = reporteItems.Count(i => i.Estado == "Vencida");
                var recaudoTotal = reporteItems.Sum(i => i.MontoPagado);
                var carteraEnRiesgo = reporteItems.Where(i => i.Estado == "Próximo a Vencer" || i.Estado == "Vencida").Sum(i => i.MontoPagado);

                return Ok(new
                {
                    KPIs = new
                    {
                        TotalSuscriptores = totalSuscriptores,
                        SuscripcionesActivas = activasCount,
                        ProximasAVencer = proximosVencerCount,
                        SuscripcionesVencidas = vencidasCount,
                        RecaudoTotalSaaS = recaudoTotal,
                        CarteraEnRiesgo = carteraEnRiesgo
                    },
                    DetalleCartera = reporteItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al procesar el reporte de cartera.", error = ex.Message });
            }
        }

        // =========================================================================
        // 🛠️ MÉTODOS AUXILIARES DE REFLEXIÓN DEFENSIVA (HU-REP01)
        // =========================================================================

        private static DateTime GetSuscripcionFechaFin(object? suscripcion, DateTime fallback)
        {
            if (suscripcion == null) return fallback;
            var t = suscripcion.GetType();
            var prop = t.GetProperty("FechaFin") 
                    ?? t.GetProperty("fecha_fin") 
                    ?? t.GetProperty("FechaVencimiento") 
                    ?? t.GetProperty("fecha_vencimiento")
                    ?? t.GetProperty("FechaExpiration");
            if (prop != null)
            {
                var val = prop.GetValue(suscripcion);
                if (val is DateTime dt) return dt;
                if (val is DateTimeOffset dto) return dto.DateTime;
                if (val != null && DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return fallback;
        }

        private static DateTime GetSuscripcionFechaInicio(object? suscripcion, DateTime fallback)
        {
            if (suscripcion == null) return fallback;
            var t = suscripcion.GetType();
            var prop = t.GetProperty("FechaInicio") ?? t.GetProperty("fecha_inicio") ?? t.GetProperty("FechaCreacion");
            if (prop != null)
            {
                var val = prop.GetValue(suscripcion);
                if (val is DateTime dt) return dt;
                if (val is DateTimeOffset dto) return dto.DateTime;
                if (val != null && DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return fallback;
        }

        private static string GetProveedorNombre(object? proveedor)
        {
            if (proveedor == null) return "Suscriptor General";
            var t = proveedor.GetType();
            var prop = t.GetProperty("NombreComercial") 
                    ?? t.GetProperty("nombre_comercial") 
                    ?? t.GetProperty("Nombre") 
                    ?? t.GetProperty("nombre")
                    ?? t.GetProperty("RazonSocial");
            var val = prop?.GetValue(proveedor)?.ToString();
            return !string.IsNullOrWhiteSpace(val) ? val : "Suscriptor General";
        }

        private static decimal GetSuscripcionMonto(object? suscripcion)
        {
            if (suscripcion == null) return 0m;
            var t = suscripcion.GetType();
            var prop = t.GetProperty("MontoPagado") ?? t.GetProperty("monto_pagado") ?? t.GetProperty("Precio") ?? t.GetProperty("Monto");
            if (prop != null)
            {
                var val = prop.GetValue(suscripcion);
                if (val != null && decimal.TryParse(val.ToString(), out var m)) return m;
            }
            return 0m;
        }

        private static string GetSuscripcionPlan(object? suscripcion, bool esIndependiente)
        {
            if (suscripcion != null)
            {
                var t = suscripcion.GetType();
                var prop = t.GetProperty("PlanAdquirido") ?? t.GetProperty("plan_adquirido") ?? t.GetProperty("Plan") ?? t.GetProperty("NombrePlan");
                var val = prop?.GetValue(suscripcion)?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
            return esIndependiente ? "Plan Independiente Pro" : "Plan Estándar Pro";
        }

        private static bool GetSuscripcionEstado(object? suscripcion)
        {
            if (suscripcion == null) return true;
            var t = suscripcion.GetType();
            var prop = t.GetProperty("Estado") ?? t.GetProperty("estado") ?? t.GetProperty("Activa") ?? t.GetProperty("activa");
            if (prop != null)
            {
                var val = prop.GetValue(suscripcion);
                if (val is bool b) return b;
                if (val != null && bool.TryParse(val.ToString(), out var parsedB)) return parsedB;
                if (val != null && int.TryParse(val.ToString(), out var parsedI)) return parsedI == 1;
            }
            return true;
        }
    }
}