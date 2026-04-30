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
        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo = "hoy", int? mes = null, int? anio = null)
        {
            // 1. Definimos el punto de partida base
            var fechaBase = fecha?.Date ?? GetBogotaTime().Date;
            
            DateTime fechaInicio;
            DateTime fechaFin;

            // 🚩 PRIORIDAD 1: Filtros de mes y año específicos (Reportes)
            if (mes.HasValue && anio.HasValue)
            {
                fechaInicio = new DateTime(anio.Value, mes.Value, 1);
                fechaFin = fechaInicio.AddMonths(1);
            }
            // 🚩 PRIORIDAD 2: Rangos de tiempo (Semana / Mes / Hoy / Mañana)
            else if (periodo.ToLower() == "semana") 
            {
                int diff = (7 + (fechaBase.DayOfWeek - DayOfWeek.Monday)) % 7;
                fechaInicio = fechaBase.AddDays(-1 * diff).Date;
                fechaFin = fechaInicio.AddDays(7);
            } 
            else if (periodo.ToLower() == "mes") 
            {
                fechaInicio = new DateTime(fechaBase.Year, fechaBase.Month, 1);
                fechaFin = fechaInicio.AddMonths(1); 
            }
            // 🛡️ BLINDAJE DE 24 HORAS: Si es hoy, mañana o se envía una fecha puntual
            else if (periodo.ToLower() == "hoy" || periodo.ToLower() == "mañana" || fecha.HasValue)
            {
                fechaInicio = fechaBase;
                fechaFin = fechaBase.AddDays(1); // Cerramos el rango a exactamente un día
            }
            else // Default de seguridad: Ventana de 24 horas para evitar fugas de datos
            {
                fechaInicio = fechaBase;
                fechaFin = fechaBase.AddDays(1);
            }

            // 2. 🛡️ QUERY MAESTRA: Traemos los datos base con sus relaciones
            var citasRango = await _context.citas 
                .Where(c => c.ProveedorId == proveedorId 
                            && c.Fecha >= fechaInicio 
                            && c.Fecha < fechaFin) 
                .Include(c => c.Servicio)
                .Include(c => c.Cliente) 
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .ToListAsync();

            // 3. 📊 ANALÍTICA DE SERVICIOS
            var topServicios = citasRango
                .Where(c => c.Estado != "cancelada")
                .GroupBy(c => c.Servicio != null ? c.Servicio.Nombre : "Servicio N/A")
                .Select(g => new {
                    Nombre = g.Key,
                    Cantidad = g.Count(),
                    Ingresos = g.Sum(x => x.PrecioPactado)
                })
                .OrderByDescending(x => x.Cantidad)
                .ToList();

            // 4. 📈 CRECIMIENTO DE CLIENTES
            var clientesNuevosData = citasRango
                .Where(c => c.Estado != "cancelada")
                .GroupBy(c => c.Fecha.Date)
                .Select(g => new {
                    Fecha = g.Key.ToString("dd/MM"),
                    Cantidad = g.Select(x => x.ClienteId).Distinct().Count()
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            // 5. 💰 PERFORMANCE FINANCIERA
            var gananciaReal = citasRango
                .Where(c => c.Estado.ToLower().Contains("completad"))
                .Sum(c => c.PrecioPactado);
            
            var gananciaEstimada = citasRango
                .Where(c => c.Estado != "cancelada")
                .Sum(c => c.PrecioPactado);

            // --- 🚀 NUEVAS ADICIONES DE LUPE (KPIs DE NEGOCIO) ---
            
            var totalIntentos = citasRango.Count;
            var tasaCancelacion = totalIntentos > 0 
                ? (double)citasRango.Count(c => c.Estado == "cancelada") / totalIntentos * 100 
                : 0;

            var totalCitasEfectivas = citasRango.Count(c => c.Estado != "cancelada");
            var ticketPromedio = totalCitasEfectivas > 0 ? gananciaEstimada / totalCitasEfectivas : 0;

            var topClientesFieles = citasRango
                .Where(c => c.Estado != "cancelada")
                .GroupBy(c => c.Cliente != null ? c.Cliente.nombre : "Anónimo")
                .Select(g => new {
                    Nombre = g.Key,
                    Visitas = g.Count(),
                    InversionTotal = g.Sum(x => x.PrecioPactado)
                })
                .OrderByDescending(x => x.InversionTotal)
                .Take(5)
                .ToList();

            return new
            {
                TipoResumen = periodo,
                // Auditamos el rango en el JSON para que puedas verlo en la consola del navegador
                RangoBusqueda = $"{fechaInicio:dd/MM/yyyy} al {fechaFin.AddDays(-1):dd/MM/yyyy}",
                TotalCitas = totalCitasEfectivas,
                NuevosClientesTotales = citasRango.Where(c => c.Estado != "cancelada").Select(c => c.ClienteId).Distinct().Count(),
                
                GananciaReal = gananciaReal,
                GananciaEstimada = gananciaEstimada,
                
                TasaCancelacion = Math.Round(tasaCancelacion, 2),
                TicketPromedio = Math.Round((double)ticketPromedio, 0),
                TopClientes = topClientesFieles,
                
                ChartServiciosPopulares = topServicios,
                ChartCrecimientoClientes = clientesNuevosData,

                ProximasCitas = citasRango.Select(c => new {
                    Id = c.Id,
                    Hora = c.Hora.ToString(@"hh\:mm"), 
                    Fecha = c.Fecha.ToString("dd/MM/yyyy"), 
                    Cliente = c.Cliente != null ? c.Cliente.nombre : "Cliente Anónimo",
                    Servicio = c.Servicio != null ? c.Servicio.Nombre : "Servicio N/A",
                    Monto = c.PrecioPactado,
                    Estado = c.Estado
                }).ToList()
            };
        }

        public async Task<object> GetResumenMensualAsync(Guid proveedorId)
        {
            var ahora = GetBogotaTime();
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            return await GetResumenDiarioAsync(proveedorId, inicioMes, "mes");
        }
    }
}