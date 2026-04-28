using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs; 
using System.Runtime.InteropServices;

namespace Turnify.Api.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly TurnifyDbContext _context;

        public DashboardService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 🛡️ ROLE: SYSTEM ARCHITECT - Sincronización horaria para evitar desfases en Docker
        private DateTime GetBogotaTime()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
            var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
        }

        // --- 📅 RESUMEN DIARIO / RANGOS (Potenciado para Analítica y Gráficas) ---
        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo = "hoy")
        {
            // 1. Definimos el punto de partida base
            var fechaBase = fecha?.Date ?? GetBogotaTime().Date;
            
            // 🚩 AJUSTE DE RANGO INTELIGENTE: Alineamos el inicio según el periodo seleccionado
            var fechaInicio = fechaBase;
            var fechaFin = fechaBase.AddDays(1);

            if (periodo == "semana") 
            {
                // Retrocedemos al lunes de la semana actual para no perder los días pasados
                int diff = (7 + (fechaBase.DayOfWeek - DayOfWeek.Monday)) % 7;
                fechaInicio = fechaBase.AddDays(-1 * diff).Date;
                fechaFin = fechaInicio.AddDays(7);
            } 
            else if (periodo == "mes") 
            {
                // 🛡️ EL ARREGLO MAESTRO: Forzamos el inicio al día 1 del mes actual
                // Así, si hoy es 27 de abril, buscará desde el 01 de abril al 01 de mayo.
                fechaInicio = new DateTime(fechaBase.Year, fechaBase.Month, 1);
                fechaFin = fechaInicio.AddMonths(1); 
            }

            // 2. 🛡️ QUERY MAESTRA: Traemos los datos base con sus relaciones
            var citasRango = await _context.citas 
                .Where(c => c.ProveedorId == proveedorId 
                            && c.Fecha >= fechaInicio 
                            && c.Fecha < fechaFin 
                            && c.Estado != "cancelada")
                .Include(c => c.Servicio)
                .Include(c => c.Cliente) 
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .ToListAsync();

            // 3. 📊 ANALÍTICA DE SERVICIOS (Para la gráfica de Dona/Barras)
            var topServicios = citasRango
                .GroupBy(c => c.Servicio != null ? c.Servicio.Nombre : "Servicio N/A")
                .Select(g => new {
                    Nombre = g.Key,
                    Cantidad = g.Count(),
                    Ingresos = g.Sum(x => x.PrecioPactado)
                })
                .OrderByDescending(x => x.Cantidad)
                .ToList();

            // 4. 📈 CRECIMIENTO DE CLIENTES (Para la gráfica de Líneas)
            // 🚩 AJUSTE TÉCNICO: Materializamos primero para evitar el Error 500 de traducción SQL
            var rawClientes = await _context.clientes
                .Where(cl => cl.fecha_creacion >= fechaInicio && cl.fecha_creacion < fechaFin)
                .Select(cl => cl.fecha_creacion)
                .ToListAsync();

            var clientesNuevosData = rawClientes
                .GroupBy(cl => cl.Date)
                .Select(g => new {
                    Fecha = g.Key.ToString("dd/MM"),
                    Cantidad = g.Count()
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            // 5. Cálculos de performance financiera
            var gananciaReal = citasRango.Where(c => c.Estado == "completada").Sum(c => c.PrecioPactado);
            var gananciaEstimada = citasRango.Sum(c => c.PrecioPactado);

            return new
            {
                TipoResumen = periodo,
                // Reportamos el rango real para que lo veas en la consola (Audit)
                RangoBusqueda = $"{fechaInicio:dd/MM} al {fechaFin.AddDays(-1):dd/MM}",
                TotalCitas = citasRango.Count,
                NuevosClientesTotales = clientesNuevosData.Sum(x => x.Cantidad),
                
                // 💰 DINERO
                GananciaReal = gananciaReal,
                GananciaEstimada = gananciaEstimada,
                
                // 📊 DATA PARA GRÁFICAS (Chart.js lista para consumir)
                ChartServiciosPopulares = topServicios,
                ChartCrecimientoClientes = clientesNuevosData,

                // 🚩 DATA PARA TABLAS Y EXPORTACIÓN (Estructura limpia para PDF/Excel)
                ProximasCitas = citasRango.Select(c => new {
                    Hora = c.Hora.ToString(@"hh\:mm"), 
                    Fecha = c.Fecha.ToString("dd/MM/yyyy"), 
                    Cliente = c.Cliente != null ? c.Cliente.nombre : "Cliente Anónimo",
                    Servicio = c.Servicio != null ? c.Servicio.Nombre : "Servicio N/A",
                    Monto = c.PrecioPactado,
                    Estado = c.Estado
                }).ToList()
            };
        }

        // --- 📊 RESUMEN MENSUAL ---
        public async Task<object> GetResumenMensualAsync(Guid proveedorId)
        {
            var ahora = GetBogotaTime();
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            return await GetResumenDiarioAsync(proveedorId, inicioMes, "mes");
        }
    }
}