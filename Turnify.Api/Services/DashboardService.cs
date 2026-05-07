using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // 🚩 MOTOR PRINCIPAL: Cálculo de métricas y agenda dinámica (Standard UTC Global)
        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo, int? mes = null, int? anio = null)
        {
            // 🛡️ BLINDAJE MUNDIAL: Cambiamos Today por UtcNow para escala global
            var fechaBase = fecha ?? DateTime.UtcNow.Date;
            var inicio = fechaBase.Date;
            var fin = inicio.AddDays(1);

            // 🕒 AJUSTE DE RANGOS SEGÚN PERIODO (Preservando lógica de bloques)
            periodo = periodo?.ToLower() ?? "hoy";

            if (periodo == "mañana")
            {
                inicio = DateTime.UtcNow.Date.AddDays(1);
                fin = inicio.AddDays(1);
            }
            else if (periodo == "semana")
            {
                // Cálculo de inicio de semana (Lunes) bajo estándar ISO
                int diff = (7 + (inicio.DayOfWeek - DayOfWeek.Monday)) % 7;
                inicio = inicio.AddDays(-1 * diff).Date;
                fin = inicio.AddDays(7);
            }
            else if (periodo == "mes" || periodo == "mensual")
            {
                // 🚩 FIX GLOBAL: Extendemos el fin para capturar citas en todos los husos horarios
                int mesConsulta = mes ?? inicio.Month;
                int anioConsulta = anio ?? inicio.Year;
                inicio = new DateTime(anioConsulta, mesConsulta, 1, 0, 0, 0, DateTimeKind.Utc);
                fin = inicio.AddMonths(1).AddDays(1); 
            }

            try
            {
                // 📊 1. CONSULTA MAESTRA (🚩 AHORA INCLUYE CANCELADAS PARA REPORTES)
                var citas = await _context.citas
                    .AsNoTracking()
                    .Include(c => c.Cliente)
                    .Include(c => c.Servicio)
                    .Where(c => c.ProveedorId == proveedorId && 
                                c.Fecha >= inicio && 
                                c.Fecha < fin) // 🛡️ Quitamos el filtro de estado para ver TODO
                    .OrderBy(c => c.Fecha)
                    .ThenBy(c => c.Hora)
                    .ToListAsync();

                // 💰 2. CÁLCULO DE MÉTRICAS (Blindaje: Las canceladas no suman a la ganancia)
                var totalCitas = citas.Count(c => c.Estado != "cancelada");
                var gananciaEstimada = citas.Where(c => c.Estado != "cancelada").Sum(c => c.PrecioPactado);
                var gananciaReal = citas.Where(c => c.Estado == "completada" || c.Estado == "confirmada")
                                        .Sum(c => c.PrecioPactado);
                
                // Clientes únicos (Solo citas válidas)
                var nuevosClientes = citas.Where(c => c.Estado != "cancelada").Select(c => c.ClienteId).Distinct().Count();

                // Cálculo de Tasa de Cancelación (Blindaje Senior)
                var canceladasCount = citas.Count(c => c.Estado == "cancelada");
                var totalConCanceladas = citas.Count;
                var tasaCancelacion = totalConCanceladas > 0 ? Math.Round((double)canceladasCount / totalConCanceladas * 100, 2) : 0;

                // 🚩 MAPEO DE RESPUESTA (Sincronización exacta con dashboard.js y reportes.js)
                return new
                {
                    tipoResumen = periodo,
                    rangoBusqueda = $"{inicio:dd/MM/yyyy} al {fin.AddDays(-1):dd/MM/yyyy} (UTC)",
                    totalCitas = totalCitas,
                    nuevosClientesTotales = nuevosClientes,
                    gananciaReal = gananciaReal,
                    gananciaEstimada = gananciaEstimada,
                    tasaCancelacion = tasaCancelacion,
                    ticketPromedio = totalCitas > 0 ? Math.Round(gananciaEstimada / totalCitas, 0) : 0,
                    
                    // 🛡️ LISTA COMPLETA (Mapeo de DTO con soporte para estados)
                    proximasCitas = citas.Select(c => new {
                        id = c.Id,
                        hora = c.Hora.ToString(@"hh\:mm"),
                        fecha = c.Fecha,
                        cliente = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                        servicio = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                        precioPactado = c.PrecioPactado,
                        estado = c.Estado,
                        codigoVerificacion = c.CodigoVerificacion
                    }).ToList(),

                    // Metadata para gráficas de servicios populares
                    chartServiciosPopulares = citas.Where(c => c.Estado != "cancelada")
                                                  .GroupBy(c => c.Servicio != null ? c.Servicio.Nombre : "Otros")
                                                  .Select(g => new { nombre = g.Key, cantidad = g.Count() })
                                                  .ToList()
                };
            }
            catch (Exception ex)
            {
                // 🚨 LOGGER DE EMERGENCIA
                Console.WriteLine($"❌ [Error Critical DashboardService]: {ex.Message}");
                return new { 
                    message = "Error al procesar métricas globales", 
                    totalCitas = 0, 
                    proximasCitas = new List<object>() 
                };
            }
        }

        // 🚩 RESUMEN MENSUAL: Puente simplificado bajo estándar UtcNow
        public async Task<object> GetResumenMensualAsync(Guid proveedorId)
        {
            var hoy = DateTime.UtcNow.Date;
            return await GetResumenDiarioAsync(proveedorId, hoy, "mes", hoy.Month, hoy.Year);
        }
    }
} 