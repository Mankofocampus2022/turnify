using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Turnify.Api.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly TurnifyDbContext _context;

        public DashboardService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 🚩 MÉTODO PRIVADO: Sincronización horaria de Bogotá (Inmunidad multi-entorno Docker UTC)
        private DateTime GetBogotaTime()
        {
            try 
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
            }
            catch 
            {
                // Backup manual UTC-5 si hay restricciones en las variables del sistema operativo
                return DateTime.UtcNow.AddHours(-5);
            }
        }

        // 🚩 MOTOR PRINCIPAL: Cálculo de métricas y agenda dinámica (Sincronizado con Colombia)
        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo, int? mes = null, int? anio = null)
        {
            // 🛡️ BLINDAJE DE PRIORIDAD: Si vienen Mes y Año, el periodo es OBLIGATORIAMENTE "mes"
            if (mes.HasValue && anio.HasValue) periodo = "mes";
            
            periodo = periodo?.ToLower() ?? "hoy";
            
            // 🚩 Sincronizamos la fecha base con la zona horaria colombiana real
            var ahoraBogota = GetBogotaTime();
            var fechaBase = fecha ?? ahoraBogota.Date;
            
            DateTime inicio, fin, inicioPrev, finPrev;

            // 🕒 1. CÁLCULO DE RANGOS DE ALTA PRECISIÓN (Actual vs Anterior para Porcentajes BI)
            // Cambiamos el uso de UtcNow.Date para priorizar la fecha procesada y enviada por el frontend
            if (periodo == "hoy" || periodo == "diario")
            {
                inicio = fechaBase.Date;
                fin = inicio.AddDays(1);
                inicioPrev = inicio.AddDays(-1);
                finPrev = inicio;
            }
            else if (periodo == "mañana")
            {
                inicio = fechaBase.Date; // Si el front ya calculó el día de mañana, lo tomamos como base limpia
                if (fecha == null) inicio = ahoraBogota.Date.AddDays(1); // Fallback si no viene parámetro
                
                fin = inicio.AddDays(1);
                inicioPrev = inicio.AddDays(-1);
                finPrev = inicio;
            }
            else if (periodo == "semana")
            {
                int diff = (7 + (inicio = fechaBase.Date).DayOfWeek - DayOfWeek.Monday) % 7;
                inicio = inicio.AddDays(-1 * diff).Date;
                fin = inicio.AddDays(7);
                inicioPrev = inicio.AddDays(-7);
                finPrev = inicio;
            }
            else // "mes" o "especifico"
            {
                int mesConsulta = mes ?? fechaBase.Month;
                int anioConsulta = anio ?? fechaBase.Year;
                inicio = new DateTime(anioConsulta, mesConsulta, 1, 0, 0, 0, DateTimeKind.Unspecified);
                fin = inicio.AddMonths(1);
                inicioPrev = inicio.AddMonths(-1);
                finPrev = inicio;
            }

            try
            {
                // 🚩 RESOLUCIÓN DE IDENTIDAD (Negocio vs Usuario)
                var prov = await _context.proveedores.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);
                
                if (prov == null) return new { message = "Negocio no encontrado", totalCitas = 0 };
                var idReal = prov.Id;

                // 📊 2. CONSULTA MAESTRA CON PROYECCIÓN (Blindada contra Nulls de raíz y optimizada OBS-01)
                var rawCitas = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Fecha >= inicio && c.Fecha < fin)
                    .Select(c => new {
                        c.Id, c.Hora, c.Fecha, c.PrecioPactado, c.Estado, c.ClienteId, c.CodigoVerificacion,
                        ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                        ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido"
                    })
                    .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                    .ToListAsync();

                // 📉 3. CONSULTA DE COMPARATIVA (BI)
                var statsPrev = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Fecha >= inicioPrev && c.Fecha < finPrev && c.Estado != "cancelada")
                    .Select(c => new { c.PrecioPactado, c.ClienteId })
                    .ToListAsync();

                // 💰 4. CÁLCULO DE MÉTRICAS ACTUALES
                var totalCitas = rawCitas.Count(c => c.Estado != "cancelada");
                decimal gananciaEstimada = rawCitas.Where(c => c.Estado != "cancelada").Sum(c => c.PrecioPactado);
                decimal gananciaReal = rawCitas.Where(c => c.Estado == "completada" || c.Estado == "confirmada").Sum(c => c.PrecioPactado);
                var nuevosClientes = rawCitas.Where(c => c.Estado != "cancelada").Select(c => c.ClienteId).Distinct().Count();
                
                var completadas = rawCitas.Count(c => c.Estado == "completada" || c.Estado == "confirmada");
                var inasistencias = rawCitas.Count(c => c.Estado == "no_asistio" || (c.Estado == "pendiente" && c.Fecha < ahoraBogota.Date));
                var canceladasCount = rawCitas.Count(c => c.Estado == "cancelada");

                // 📈 5. LÓGICA DE PORCENTAJES
                decimal sumaPrev = statsPrev.Sum(s => s.PrecioPactado);
                double percGanancia = sumaPrev > 0 ? Math.Round((double)((gananciaEstimada - sumaPrev) / sumaPrev) * 100, 1) : 0;
                
                int clientesPrevCount = statsPrev.Select(s => s.ClienteId).Distinct().Count();
                double percClientes = clientesPrevCount > 0 ? Math.Round((double)(nuevosClientes - clientesPrevCount) / clientesPrevCount * 100, 1) : 0;

                // 👥 6. ANÁLISIS DE RETENCIÓN
                var fechaCorteRetencion = ahoraBogota.Date.AddMonths(-1);
                var clientesEnRiesgo = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Estado == "completada")
                    .GroupBy(c => c.ClienteId)
                    .Where(g => g.Max(c => c.Fecha) < fechaCorteRetencion)
                    .CountAsync();

                // 🚩 7. MAPEO DE RESPUESTA FINAL
                return new {
                    tipoResumen = periodo,
                    rangoBusqueda = $"{inicio:dd/MM/yyyy} al {fin.AddDays(-1):dd/MM/yyyy} (Colombia Time)",
                    totalCitas, tendenciaCitas = percClientes,
                    nuevosClientesTotales = nuevosClientes, clientesEnRiesgo,
                    gananciaReal, gananciaEstimada, crecimientoIngresos = percGanancia,
                    tasaAsistencia = totalCitas > 0 ? Math.Round((double)completadas / totalCitas * 100, 1) : 0,
                    tasaInasistencia = totalCitas > 0 ? Math.Round((double)inasistencias / totalCitas * 100, 1) : 0,
                    tasaCancelacion = (totalCitas + canceladasCount) > 0 ? Math.Round((double)canceladasCount / (totalCitas + canceladasCount) * 100, 1) : 0,
                    ticketPromedio = totalCitas > 0 ? Math.Round(gananciaEstimada / totalCitas, 0) : 0,
                    proximasCitas = rawCitas.Select(c => new {
                        id = c.Id, hora = c.Hora.ToString(@"hh\:mm"), fecha = c.Fecha,
                        cliente = c.ClienteNombre, servicio = c.ServicioNombre,
                        precioPactado = c.PrecioPactado, estado = c.Estado, codigoVerificacion = c.CodigoVerificacion
                    }).ToList(),
                    chartServiciosPopulares = rawCitas.Where(c => c.Estado != "cancelada")
                                                  .GroupBy(c => c.ServicioNombre)
                                                  .Select(g => new { nombre = g.Key, cantidad = g.Count() })
                                                  .ToList()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Error Critical DashboardService]: {ex.Message}");
                return new { message = "Error al procesar métricas", totalCitas = 0, proximasCitas = new List<object>() };
            }
        }

        public async Task<object> GetResumenMensualAsync(Guid proveedorId)
        {
            var ahoraColombia = GetBogotaTime().Date;
            return await GetResumenDiarioAsync(proveedorId, ahoraColombia, "mes", ahoraColombia.Month, ahoraColombia.Year);
        }
    }
}