using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces; // 👈 Agregado para consumir la interfaz del Bot de WhatsApp
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // 👈 Agregado para la resolución correcta de Scopes

namespace Turnify.Api.Workers
{
    public class CitaCancellationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CitaCancellationWorker> _logger;
        // Se ejecuta cada 10 minutos para barrer inasistencias y despachar alertas proactivas
        private readonly TimeSpan _periodoEjecucion = TimeSpan.FromMinutes(10); 

        public CitaCancellationWorker(IServiceProvider serviceProvider, ILogger<CitaCancellationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 [Turnify Background] El motor unificado de cancelación y recordatorios 24h ha despertado.");
            using var timer = new PeriodicTimer(_periodoEjecucion);

            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("🔍 [Turnify Background] Iniciando ciclos de control automatizado...");
                    
                    // 1. Barrido de citas vencidas (Inasistencias)
                    await ProcesarCitasVencidasAsync();

                    // 2. 🧠 KILLER FIX BUG 5: Despacho automático de recordatorios con 24 horas de antelación
                    await ProcesarRecordatorios24HorasAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ [Turnify Background Error] Error en el ciclo del Worker: {ex.Message}");
                }
            }
        }

        private async Task ProcesarCitasVencidasAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TurnifyDbContext>();

            var ahoraBogota = GetBogotaTime();
            var fechaHoy = ahoraBogota.Date;
            var horaActual = ahoraBogota.TimeOfDay;
            
            // 🧠 MODIFICACIÓN CRÍTICA: Cambiado de 15 a 10 minutos de gracia exactos según tu regla de negocio
            var horaLimite = horaActual.Subtract(TimeSpan.FromMinutes(10)); // 10 min de gracia

            // 🧠 ADICIÓN DE LOG: Para diagnosticar en vivo qué horas límites está calculando el contenedor de Docker
            _logger.LogInformation($"🔎 [Cron Inasistencias] Evaluando citas pendientes anteriores al día {fechaHoy:dd/MM/yyyy} o de hoy que debían llegar antes de las {horaLimite:hh\\:mm}");

            // Agregamos .Date a la query para que SQL Server compare limpiamente solo las fechas sin milisegundos de desfase
            var citasVencidas = await context.citas
                .Where(c => c.Estado == "pendiente" && 
                            (c.Fecha.Date < fechaHoy || (c.Fecha.Date == fechaHoy && c.Hora < horaLimite)))
                .ToListAsync();

            if (!citasVencidas.Any())
            {
                _logger.LogInformation("✅ [Turnify Background] Sin inasistencias por procesar en este ciclo.");
                return;
            }

            _logger.LogWarning($"⚠️ [Turnify Background] Detectadas {citasVencidas.Count} citas vencidas. Ejecutando cancelación masiva...");

            foreach (var cita in citasVencidas)
            {
                cita.Estado = "cancelada";
                cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones) 
                    ? "Cancelada automáticamente por el sistema debido a inasistencia." 
                    : $"{cita.Observaciones} | Cancelada por inasistencia.";

                // 🧠 ADICIÓN DE LOG VISUAL: Ahora verás de forma explícita en los logs de tu consola cada cita eliminada
                _logger.LogWarning($"❌ [Inasistencia Aplicada] Cita ID: {cita.Id} programada para las {cita.Hora} fue cancelada por superar los 10 minutos de retraso.");
            }

            await context.SaveChangesAsync();
        }

        // 🧠 KILLER FIX BUG 5: Implementación transaccional de alertas proactivas para el día siguiente
        private async Task ProcesarRecordatorios24HorasAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TurnifyDbContext>();
            
            // 🧠 ADICIÓN DE QUALITY: Resolvemos el servicio de WhatsApp dentro del Scope transaccional
            var whatsAppService = scope.ServiceProvider.GetService<IWhatsAppService>();

            var ahoraBogota = GetBogotaTime();
            var fechaManana = ahoraBogota.Date.AddDays(1); // Citas de mañana (24 horas antes)

            // Buscamos citas pendientes para mañana que no posean la etiqueta de control en observaciones
            var citasParaRecordar = await context.citas
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Proveedor)
                .Where(c => c.Estado == "pendiente" && c.Fecha == fechaManana && 
                            (c.Observaciones == null || !c.Observaciones.Contains("Recordatorio 24h enviado")))
                .ToListAsync();

            if (!citasParaRecordar.Any())
            {
                _logger.LogInformation("✅ [Turnify Background] Recordatorios 24h al día. No hay pendientes por notificar.");
                return;
            }

            _logger.LogInformation($"🔔 [Turnify Background] Se detectaron {citasParaRecordar.Count} citas para mañana sin notificar. Despachando alertas...");

            foreach (var cita in citasParaRecordar)
            {
                // 🛡️ ADICIÓN DE QUALITY: Aislamos cada envío en un try-catch interno. 
                // Si una notificación falla por un celular mal escrito, el bucle sigue adelante con los demás clientes.
                try 
                {
                    if (cita.Cliente != null)
                    {
                        // Conservamos tu trazabilidad en consola intacta sin tocar una sola letra
                        Console.WriteLine("\n--------------------------------------------------");
                        Console.WriteLine($"📱 [ALERTA PROACTIVA DE AGENDAMIENTO WHATSAPP - 24H ANTES]");
                        Console.WriteLine($"Celular Cliente: {cita.Cliente.telefono} | Correo: {cita.Cliente.email}");
                        Console.WriteLine($"Hola {cita.Cliente.nombre}, te recordamos tu cita de mañana {cita.Fecha:dd/MM/yyyy} a las {cita.Hora:hh\\:mm}.");
                        Console.WriteLine($"Profesional: {cita.Proveedor?.NombreComercial} | Servicio: {cita.Servicio?.Nombre} | Modalidad: {cita.Modalidad?.ToUpper()}");
                        Console.WriteLine($"🔑 TU CÓDIGO DE CHECK-IN ES: {cita.CodigoVerificacion}");
                        Console.WriteLine("--------------------------------------------------\n");

                        // 🔥 ADICIÓN MAESTRA: Ejecución real del disparo del recordatorio saliente por WhatsApp
                        if (whatsAppService != null)
                        {
                            await whatsAppService.EnviarRecordatorioCitaAsync(cita.Id);
                        }
                    }

                    // Inyectamos la etiqueta de control para asegurar la idempotencia del proceso (No enviar spam)
                    cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones)
                        ? "Recordatorio 24h enviado automáticamente por el sistema."
                        : $"{cita.Observaciones} | Recordatorio 24h enviado.";
                }
                catch (Exception exInner)
                {
                    _logger.LogError($"⚠️ [Turnify Background] Error procesando notificación individual para la cita {cita.Id}: {exInner.Message}");
                }
            }

            // Persistimos los cambios confirmando que todo el lote fue procesado
            await context.SaveChangesAsync();
            _logger.LogInformation("🎉 [Turnify Background] Despacho masivo de recordatorios de 24 horas completado con éxito.");
        }

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
                return DateTime.UtcNow.AddHours(-5); // Fallback manual de zona horaria
            }
        }
    }
}