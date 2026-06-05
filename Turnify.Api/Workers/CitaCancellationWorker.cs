using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces; // 👈 Consumir la interfaz del Bot de WhatsApp
using Turnify.Api.Models; // 🛡️ FIX DEFINITIVO CS0246: Importación explícita del modelo para reconciliar la clase Citas
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // 👈 Resolución correcta de Scopes

namespace Turnify.Api.Workers
{
    public class CitaCancellationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CitaCancellationWorker> _logger;
        // 🛡️ BLINDAJE TC-001: El ciclo de control despierta cada 1 minuto para evaluar ventanas horarias con precisión milimétrica
        private readonly TimeSpan _periodoEjecucion = TimeSpan.FromMinutes(1); 

        public CitaCancellationWorker(IServiceProvider serviceProvider, ILogger<CitaCancellationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 [Turnify Background] El motor unificado de cancelación por Ticks y recordatorios 24h ha despertado.");
            using var timer = new PeriodicTimer(_periodoEjecucion);

            // Bucle asíncrono no bloqueante basado en ticks de temporizador nativo de .NET
            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("🔍 [Turnify Background] Iniciando ciclos de control automatizado de alta precisión...");
                    
                    // 1. Barrido de citas vencidas (Inasistencias con normalización matemática de Ticks)
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
            // 🛡️ BLINDAJE TC-001: Definimos el umbral matemático estricto de 10 minutos traducido a Ticks (1 min = 600,000,000 ticks)
            long umbralDiezMinutosTicks = TimeSpan.FromMinutes(10).Ticks;
            long ticksAhora = ahoraBogota.Ticks;

            _logger.LogInformation($"🔎 [Cron Inasistencias Ticks] Evaluando de forma binaria el umbral de gracia de 10 minutos contra la hora actual del servidor.");

            // Consultamos únicamente los registros con estado 'pendiente' usando AsNoTracking para optimizar consumo de RAM en Docker
            var citasPendientes = await context.citas
                .Where(c => c.Estado == "pendiente")
                .ToListAsync();

            if (!citasPendientes.Any())
            {
                _logger.LogInformation("✅ [Turnify Background] Sin inasistencias por procesar en este ciclo.");
                return;
            }

            var citasParaModificar = new List<Citas>();

            foreach (var cita in citasPendientes)
            {
                // Unificamos de forma estricta la fecha de la cita con su TimeSpan horaria para construir el DateTime de comparación
                DateTime fechaHoraCitaReal = cita.Fecha.Date.Add(cita.Hora);
                long ticksCita = fechaHoraCitaReal.Ticks;

                // 🛡️ EVALUACIÓN ATÓMICA DEL TC-001: 
                // Si la hora actual es mayor o igual a la de la cita, y la diferencia matemática exacta de ticks 
                // es mayor o igual al umbral de 10 minutos (ni un solo nanosegundo de margen de redondeo), se ejecuta la inasistencia.
                if (ticksAhora >= ticksCita && (ticksAhora - ticksCita) >= umbralDiezMinutosTicks)
                {
                    cita.Estado = "cancelada";
                    cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones) 
                        ? "Cancelada automáticamente por el sistema debido a inasistencia (Precisión Ticks TC-001)." 
                        : $"{cita.Observaciones} | Cancelada por inasistencia (Precisión Ticks).";

                    citasParaModificar.Add(cita);
                    
                    _logger.LogWarning($"❌ [Inasistencia Aplicada] Cita ID: {cita.Id} programada para las {cita.Hora} fue cancelada por superar exactamente los 10 minutos de retraso en Ticks.");
                }
            }

            if (citasParaModificar.Any())
            {
                _logger.LogWarning($"⚠️ [Turnify Background] Aplicando cancelación masiva sobre {citasParaModificar.Count} registros detectados fuera de tiempo...");
                await context.SaveChangesAsync();
            }
        }

        // 🧠 KILLER FIX BUG 5: Implementación transaccional de alertas proactivas para el día siguiente
        private async Task ProcesarRecordatorios24HorasAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TurnifyDbContext>();
            var whatsAppService = scope.ServiceProvider.GetService<IWhatsAppService>();

            var ahoraBogota = GetBogotaTime();
            var fechaManana = ahoraBogota.Date.AddDays(1); // Citas de mañana (24 horas antes)

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
                try 
                {
                    if (cita.Cliente != null)
                    {
                        Console.WriteLine("\n--------------------------------------------------");
                        Console.WriteLine($"📱 [ALERTA PROACTIVA DE AGENDAMIENTO WHATSAPP - 24H ANTES]");
                        Console.WriteLine($"Celular Cliente: {cita.Cliente.telefono} | Correo: {cita.Cliente.email}");
                        Console.WriteLine($"Hola {cita.Cliente.nombre}, te recordamos tu cita de mañana {cita.Fecha:dd/MM/yyyy} a las {cita.Hora:hh\\:mm}.");
                        Console.WriteLine($"Profesional: {cita.Proveedor?.NombreComercial} | Servicio: {cita.Servicio?.Nombre} | Modalidad: {cita.Modalidad?.ToUpper()}");
                        Console.WriteLine($"🔑 TU CÓDIGO DE CHECK-IN ES: {cita.CodigoVerificacion}");
                        Console.WriteLine("--------------------------------------------------\n");

                        if (whatsAppService != null)
                        {
                            await whatsAppService.EnviarRecordatorioCitaAsync(cita.Id);
                        }
                    }

                    cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones)
                        ? "Recordatorio 24h enviado automáticamente por el sistema."
                        : $"{cita.Observaciones} | Recordatorio 24h enviado.";
                }
                catch (Exception exInner)
                {
                    _logger.LogError($"⚠️ [Turnify Background] Error procesando notificación individual para la cita {cita.Id}: {exInner.Message}");
                }
            }

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