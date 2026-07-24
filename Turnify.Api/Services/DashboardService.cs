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

        // 🚩 MOTOR PRINCIPAL: Cálculo de métricas y agenda dinámica (GLOBALIZADO)
        // La variable 'fecha' ahora es inyectada por el Controller basada en la zona horaria del cliente (Ej: Europe/Madrid)
        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo, int? mes = null, int? anio = null)
        {
            // 🛡️ BLINDAJE DE PRIORIDAD: Si vienen Mes y Año, el periodo es OBLIGATORIAMENTE "mes"
            if (mes.HasValue && anio.HasValue) periodo = "mes";
            
            periodo = periodo?.ToLower() ?? "hoy";
            
            // 🌐 GLOBALIZACIÓN: Asumimos que la fecha que entra ya es el "Hoy" local del usuario. 
            // Si por alguna razón llega nula, hacemos un fallback seguro a la hora UTC.
            var fechaBase = fecha ?? DateTime.UtcNow.Date;
            
            DateTime inicio, fin, inicioPrev, finPrev;

            // 🕒 1. CÁLCULO DE RANGOS DE ALTA PRECISIÓN (Basado en la fecha local del usuario)
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
                if (fecha == null) inicio = DateTime.UtcNow.Date.AddDays(1); // Fallback si no viene parámetro
                
                fin = inicio.AddDays(1);
                inicioPrev = inicio.AddDays(-1);
                finPrev = inicio;
            }
            else if (periodo == "semana")
            {
                int diff = (7 + (int)(inicio = fechaBase.Date).DayOfWeek - (int)DayOfWeek.Monday) % 7;
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
                        ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                        // 🚀 HU 001 & HU 003: Inyección UI, Nombre del empleado, estación y esquema de contratación
                        EmpleadoAsignado = c.Empleado != null ? c.Empleado.Nombre : "Sin asignar",
                        Estacion = c.Estacion != null ? c.Estacion.Nombre : "Local",
                        TipoContratoEmpleado = c.Empleado != null ? c.Empleado.TipoContrato : "comision",
                        ValorContratoEmpleado = c.Empleado != null ? c.Empleado.ValorContrato : 0
                    })
                    .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                    .ToListAsync();

                // 📉 3. CONSULTA DE COMPARATIVA (BI)
                var statsPrev = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Fecha >= inicioPrev && c.Fecha < finPrev && c.Estado != "cancelada")
                    .Select(c => new { c.PrecioPactado, c.ClienteId })
                    .ToListAsync();

                // 💰 4. CÁLCULO DE MÉTRICAS ACTUALES & HU-02 (FINANCIERO NEGOCIO)
                var totalCitas = rawCitas.Count(c => c.Estado != "cancelada");
                decimal gananciaEstimada = rawCitas.Where(c => c.Estado != "cancelada").Sum(c => c.PrecioPactado);
                decimal gananciaReal = rawCitas.Where(c => c.Estado == "completada" || c.Estado == "confirmada").Sum(c => c.PrecioPactado);
                
                // HU-02 CA1 & CA2: Margen neto descontando comisiones a colaboradores
                decimal ingresoProyectadoNegocio = rawCitas
                    .Where(c => c.Estado != "cancelada")
                    .Sum(c => {
                        var tipo = (c.TipoContratoEmpleado ?? "").ToLower();
                        if (tipo.Contains("silla") || tipo.Contains("fijo") || tipo.Contains("arriendo"))
                        {
                            return c.PrecioPactado; // Silla fija no descuenta comisión por servicio
                        }
                        decimal pctComision = c.ValorContratoEmpleado > 0 ? c.ValorContratoEmpleado : 0;
                        decimal valorComision = c.PrecioPactado * (pctComision / 100);
                        return c.PrecioPactado - valorComision;
                    });

                // 💈 HU-07: Identificar clientes nuevos dentro del agendamiento
                var clientesIdsPeriodo = rawCitas
                    .Where(c => c.Estado != "cancelada" && c.ClienteId != Guid.Empty)
                    .Select(c => c.ClienteId)
                    .Distinct()
                    .ToList();

                var clientesAntiguosIds = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && clientesIdsPeriodo.Contains(c.ClienteId) && c.Fecha < inicio && c.Estado != "cancelada")
                    .Select(c => c.ClienteId)
                    .Distinct()
                    .ToListAsync();

                var setAntiguos = new HashSet<Guid>(clientesAntiguosIds);
                var nuevosClientes = rawCitas.Where(c => c.Estado != "cancelada" && c.ClienteId != Guid.Empty && !setAntiguos.Contains(c.ClienteId)).Select(c => c.ClienteId).Distinct().Count();
                
                var completadas = rawCitas.Count(c => c.Estado == "completada" || c.Estado == "confirmada");
                var inasistencias = rawCitas.Count(c => c.Estado == "no_asistio" || (c.Estado == "pendiente" && c.Fecha < fechaBase.Date));
                var canceladasCount = rawCitas.Count(c => c.Estado == "cancelada");

                // 📈 5. LÓGICA DE PORCENTAJES
                decimal sumaPrev = statsPrev.Sum(s => s.PrecioPactado);
                double percGanancia = sumaPrev > 0 ? Math.Round((double)((gananciaEstimada - sumaPrev) / sumaPrev) * 100, 1) : 0;
                
                int clientesPrevCount = statsPrev.Select(s => s.ClienteId).Distinct().Count();
                double percClientes = clientesPrevCount > 0 ? Math.Round((double)(nuevosClientes - clientesPrevCount) / clientesPrevCount * 100, 1) : 0;

                // 👥 6. ANÁLISIS DE RETENCIÓN
                var fechaCorteRetencion = fechaBase.Date.AddMonths(-1);
                var clientesEnRiesgo = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Estado == "completada")
                    .GroupBy(c => c.ClienteId)
                    .Where(g => g.Max(c => c.Fecha) < fechaCorteRetencion)
                    .CountAsync();

                // 🚩 7. MAPEO DE RESPUESTA FINAL (GLOBAL)
                return new {
                    tipoResumen = periodo,
                    rangoBusqueda = $"{inicio:dd/MM/yyyy} al {fin.AddDays(-1):dd/MM/yyyy} (Local Time)",
                    totalCitas, tendenciaCitas = percClientes,
                    nuevosClientesTotales = nuevosClientes, clientesEnRiesgo,
                    gananciaReal, gananciaEstimada, 
                    ingresoProyectadoNegocio, // HU-02
                    crecimientoIngresos = percGanancia,
                    tasaAsistencia = totalCitas > 0 ? Math.Round((double)completadas / totalCitas * 100, 1) : 0,
                    tasaInasistencia = totalCitas > 0 ? Math.Round((double)inasistencias / totalCitas * 100, 1) : 0,
                    tasaCancelacion = (totalCitas + canceladasCount) > 0 ? Math.Round((double)canceladasCount / (totalCitas + canceladasCount) * 100, 1) : 0,
                    ticketPromedio = totalCitas > 0 ? Math.Round(gananciaEstimada / totalCitas, 0) : 0,
                    proximasCitas = rawCitas.Select(c => {
                        var tipoContrato = (c.TipoContratoEmpleado ?? "").ToLower();
                        bool esSillaFija = tipoContrato.Contains("silla") || tipoContrato.Contains("fijo") || tipoContrato.Contains("arriendo");
                        
                        return new {
                            id = c.Id, hora = c.Hora.ToString(@"hh\:mm"), fecha = c.Fecha,
                            cliente = c.ClienteNombre, servicio = c.ServicioNombre,
                            precioPactado = c.PrecioPactado, estado = c.Estado, codigoVerificacion = c.CodigoVerificacion,
                            empleadoAsignado = c.EmpleadoAsignado,
                            estacion = c.Estacion,
                            tipoContratoEmpleado = c.TipoContratoEmpleado,
                            porcentajeComision = c.ValorContratoEmpleado,
                            // 🚀 HU-03 CA1 & CA2: Valores y Estado para la alerta de Silla
                            precioSilla = esSillaFija ? c.ValorContratoEmpleado : (decimal?)null,
                            estadoPagoSilla = esSillaFija ? "Al día" : null,
                            // 💈 HU-07 CA1: Banderín visual para el frontend
                            esNuevoCliente = c.ClienteId != Guid.Empty && !setAntiguos.Contains(c.ClienteId)
                        };
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
            // 🌐 GLOBALIZACIÓN: Si se llama directo (sin fecha local), usamos UTC como estándar neutro.
            var ahoraGlobal = DateTime.UtcNow.Date;
            return await GetResumenDiarioAsync(proveedorId, ahoraGlobal, "mes", ahoraGlobal.Month, ahoraGlobal.Year);
        }

        // 🚀🚀🚀 HU 001: EL CEREBRO FINANCIERO (NUEVO MÉTODO GLOBAL) 🚀🚀🚀
        public async Task<object> GetLiquidacionStaffAsync(Guid empleadoId, DateTime fechaBase, string periodo, int? mes = null, int? anio = null)
        {
            // 1. Obtener los datos del contrato del empleado
            var empleado = await _context.empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == empleadoId);

            if (empleado == null) return new { message = "Empleado no encontrado." };

            // 2. Calcular las fechas locales
            DateTime inicio, fin;
            if (periodo == "hoy" || periodo == "diario") { inicio = fechaBase.Date; fin = inicio.AddDays(1); }
            else if (periodo == "semana") { int diff = (7 + (int)(inicio = fechaBase.Date).DayOfWeek - (int)DayOfWeek.Monday) % 7; inicio = inicio.AddDays(-1 * diff).Date; fin = inicio.AddDays(7); }
            else { int m = mes ?? fechaBase.Month; int a = anio ?? fechaBase.Year; inicio = new DateTime(a, m, 1, 0, 0, 0, DateTimeKind.Unspecified); fin = inicio.AddMonths(1); }

            // 3. Consultar SOLO las citas asignadas a este empleado en el periodo
            var misCitas = await _context.citas
                .AsNoTracking()
                .Where(c => c.EmpleadoId == empleadoId && c.Fecha >= inicio && c.Fecha < fin && c.Estado != "cancelada")
                .Select(c => new {
                    c.Id, c.Hora, c.Fecha, c.PrecioPactado, c.Estado, c.ClienteId,
                    ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "No registrado",
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "No definido"
                })
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .ToListAsync();

            // 4. LÓGICA DE COMISIONES (La magia financiera)
            decimal totalGenerado = misCitas.Where(c => c.Estado == "completada" || c.Estado == "confirmada").Sum(c => c.PrecioPactado);
            decimal miComisionNeta = 0;

            if (empleado.TipoContrato.ToLower() == "porcentaje")
            {
                miComisionNeta = totalGenerado * (empleado.ValorContrato / 100);
            }
            else if (empleado.TipoContrato.ToLower() == "fijo")
            {
                miComisionNeta = empleado.ValorContrato; 
            }

            // 5. Devolver un Dashboard "Lite" exclusivo para el barbero
            return new
            {
                tipoResumen = periodo,
                empleado = empleado.Nombre,
                contrato = $"{empleado.TipoContrato} ({empleado.ValorContrato}{(empleado.TipoContrato.ToLower() == "porcentaje" ? "%" : "$")})",
                totalCitasAtendidas = misCitas.Count,
                ventasTotalesGeneradas = totalGenerado, 
                miComisionLiquidar = Math.Round(miComisionNeta, 2), 
                gananciaParaLocal = Math.Round(totalGenerado - miComisionNeta, 2), 
                misCitas = misCitas.Select(c => new {
                    id = c.Id, hora = c.Hora.ToString(@"hh\:mm"), fecha = c.Fecha,
                    cliente = c.ClienteNombre, servicio = c.ServicioNombre,
                    precioCobrado = c.PrecioPactado, estado = c.Estado
                }).ToList()
            };
        }

        // =========================================================================
        // 💈 HU-06 & HU-07: MÓDULO EXCLUSIVO PARA PROFESIONAL INDEPENDIENTE
        // =========================================================================
        public async Task<object> GetDashboardIndependienteAsync(Guid proveedorId, DateTime fechaBase, string periodo = "diario", int? mes = null, int? anio = null)
        {
            if (mes.HasValue && anio.HasValue) periodo = "mes";
            periodo = periodo?.ToLower() ?? "diario";

            DateTime inicio, fin;

            // 1. Cálculo del rango temporal basado en la fecha local delivered
            if (periodo == "hoy" || periodo == "diario")
            {
                inicio = fechaBase.Date;
                fin = inicio.AddDays(1);
            }
            else if (periodo == "semana")
            {
                int diff = (7 + (int)(inicio = fechaBase.Date).DayOfWeek - (int)DayOfWeek.Monday) % 7;
                inicio = inicio.AddDays(-1 * diff).Date;
                fin = inicio.AddDays(7);
            }
            else
            {
                int mesConsulta = mes ?? fechaBase.Month;
                int anioConsulta = anio ?? fechaBase.Year;
                inicio = new DateTime(anioConsulta, mesConsulta, 1, 0, 0, 0, DateTimeKind.Unspecified);
                fin = inicio.AddMonths(1);
            }

            try
            {
                // Identificar al proveedor
                var prov = await _context.proveedores.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == proveedorId || p.UsuarioId == proveedorId);

                if (prov == null) return new { message = "Profesional independiente no encontrado", totalCitas = 0 };
                var idReal = prov.Id;

                // 2. Traer todas las citas del período asignadas al proveedor independiente
                var rawCitas = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && c.Fecha >= inicio && c.Fecha < fin)
                    .Select(c => new {
                        c.Id,
                        c.Hora,
                        c.Fecha,
                        c.PrecioPactado,
                        c.Estado,
                        c.ClienteId,
                        c.CodigoVerificacion,
                        ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                        ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido"
                    })
                    .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                    .ToListAsync();

                // 3. HU-07 (CA1 & CA2): Identificar cuáles clientes son NUEVOS vs HABITUALES
                var clientesIdsPeriodo = rawCitas
                    .Where(c => c.Estado != "cancelada" && c.ClienteId != Guid.Empty)
                    .Select(c => c.ClienteId)
                    .Distinct()
                    .ToList();

                var clientesAntiguosIds = await _context.citas
                    .AsNoTracking()
                    .Where(c => c.ProveedorId == idReal && clientesIdsPeriodo.Contains(c.ClienteId) && c.Fecha < inicio && c.Estado != "cancelada")
                    .Select(c => c.ClienteId)
                    .Distinct()
                    .ToListAsync();

                var setAntiguos = new HashSet<Guid>(clientesAntiguosIds);

                var listaCitasIndependiente = rawCitas.Select(c => new {
                    id = c.Id,
                    hora = c.Hora.ToString(@"hh\:mm"),
                    fecha = c.Fecha,
                    cliente = c.ClienteNombre,
                    servicio = c.ServicioNombre,
                    precioPactado = c.PrecioPactado,
                    estado = c.Estado,
                    codigoVerificacion = c.CodigoVerificacion,
                    // HU-07 CA1: El cliente es nuevo si NO tiene citas previas registradas
                    esNuevoCliente = c.ClienteId != Guid.Empty && !setAntiguos.Contains(c.ClienteId)
                }).ToList();

                // 4. HU-06 (CA1 & CA2): CÁLCULO DE INGRESOS 100% BRUTOS (Sin descuento de comisión)
                var citasValidas = rawCitas.Where(c => c.Estado != "cancelada").ToList();
                decimal ingresosProyectadosBrutos = citasValidas.Sum(c => c.PrecioPactado);
                decimal ingresosRealesBrutos = citasValidas.Where(c => c.Estado == "completada" || c.Estado == "confirmada").Sum(c => c.PrecioPactado);

                // HU-07 CA2: Total de nuevos clientes en el periodo
                int totalNuevosClientes = listaCitasIndependiente.Where(c => c.estado != "cancelada" && c.esNuevoCliente).Select(c => c.cliente).Distinct().Count();

                return new
                {
                    tipoResumen = periodo,
                    rangoBusqueda = $"{inicio:dd/MM/yyyy} al {fin.AddDays(-1):dd/MM/yyyy} (Local Time)",
                    totalCitas = citasValidas.Count,
                    ingresosProyectadosBrutos, // HU-06: 100% bruto proyectado
                    ingresosRealesBrutos,      // HU-06: 100% bruto realizado
                    totalNuevosClientes,       // HU-07: Conteo total de clientes nuevos
                    citas = listaCitasIndependiente
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Error Critical DashboardIndependiente]: {ex.Message}");
                return new { message = "Error al procesar el dashboard independiente", totalCitas = 0, citas = new List<object>() };
            }
        }
    }
}