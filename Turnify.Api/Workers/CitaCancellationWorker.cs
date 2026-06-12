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
            _logger.LogInformation("🚀 [Turnify Background] El motor unificado de internacionalización y cancelación por Ticks ha despertado.");
            using var timer = new PeriodicTimer(_periodoEjecucion);

            // Bucle asíncrono no bloqueante basado en ticks de temporizador nativo de .NET
            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("🔍 [Turnify Background] Iniciando ciclos de control automatizado de alta precisión mundial...");
                    
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

            // 🌐 INTERNACIONALIZACIÓN CORE: Capturamos el momento absoluto en el universo (UTC)
            var ahoraUtc = DateTimeOffset.UtcNow;
            
            // 🛡️ BLINDAJE TC-001: Definimos el umbral matemático estricto de 10 minutos traducido a Ticks (1 min = 600,000,000 ticks)
            long umbralDiezMinutosTicks = TimeSpan.FromMinutes(10).Ticks;
            long ticksAhora = ahoraUtc.Ticks;

            _logger.LogInformation($"🔎 [Cron Inasistencias Ticks] Evaluando de forma binaria el umbral de gracia de 10 minutos contra el tiempo universal UTC.");

            // Consultamos únicamente los registros con estado 'pendiente'
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
                // 🌐 RECONCILIACIÓN NATAL DE OFFSET: Como cita.Fecha ya es un DateTimeOffset real extraído de la BD,
                // simplemente acoplamos el TimeSpan de .Hora para calcular el momento exacto programado de la cita.
                var fechaHoraCitaReal = cita.Fecha.Date.Add(cita.Hora);
                var datetimeOffsetCitaReal = new DateTimeOffset(fechaHoraCitaReal, cita.Fecha.Offset);
                
                // Convertimos la cita a escala UTC absoluta para comparar peras con peras en el universo de Ticks
                long ticksCita = datetimeOffsetCitaReal.ToUniversalTime().Ticks;

                // 🛡️ EVALUACIÓN ATÓMICA DEL TC-001: 
                // Si el tiempo actual del universo superó el momento exacto pactado por el comercio + sus 10 min de gracia
                if (ticksAhora >= ticksCita && (ticksAhora - ticksCita) >= umbralDiezMinutosTicks)
                {
                    cita.Estado = "cancelada";
                    cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones) 
                        ? "Cancelada automáticamente por el sistema debido a inasistencia (Precisión Ticks MUNDIAL TC-001)." 
                        : $"{cita.Observaciones} | Cancelada por inasistencia (Precisión Ticks Mundial).";

                    citasParaModificar.Add(cita);
                    
                    _logger.LogWarning($"❌ [Inasistencia Aplicada] Cita ID: {cita.Id} programada para las {cita.Hora} fue cancelada por superar exactamente los 10 minutos de retraso en escala universal.");
                }
            }

            if (citasParaModificar.Any())
            {
                _logger.LogWarning($"⚠️ [Turnify Background] Aplicando cancelación masiva sobre {citasParaModificar.Count} registros detectados fuera de tiempo...");
                await context.SaveChangesAsync();
            }
        }

        // 🧠 KILLER FIX BUG 5: Implementación transaccional de alertas proactivas para el día siguiente mundial
        private async Task ProcesarRecordatorios24HorasAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TurnifyDbContext>();
            var whatsAppService = scope.ServiceProvider.GetService<IWhatsAppService>();

            // 🌐 INTERNACIONALIZACIÓN: Trabajamos sobre el tiempo real universal absoluto
            var ahoraUtc = DateTimeOffset.UtcNow;

            var citasParaRecordar = await context.citas
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Proveedor)
                .Where(c => c.Estado == "pendiente" && 
                            (c.Observaciones == null || !c.Observaciones.Contains("Recordatorio 24h enviado")))
                .ToListAsync();

            if (!citasParaRecordar.Any())
            {
                _logger.LogInformation("✅ [Turnify Background] Recordatorios 24h al día. No hay pendientes por notificar.");
                return;
            }

            var modificarAlerta = false;

            foreach (var cita in citasParaRecordar)
            {
                try 
                {
                    // 🌐 EVALUACIÓN DEL HUSO HORARIO DE LA CITA:
                    // Convertimos la hora UTC actual al desfase/offset específico con el que se creó esta cita en particular
                    var horaLocalSegunCita = ahoraUtc.ToOffset(cita.Fecha.Offset);
                    var fechaMananaSegunComercio = horaLocalSegunCita.Date.AddDays(1);

                    // Si la fecha de la cita corresponde exactamente al día de mañana del comercio, se dispara el trigger
                    if (cita.Fecha.Date == fechaMananaSegunComercio)
                    {
                        if (cita.Cliente != null)
                        {
                            Console.WriteLine("\n--------------------------------------------------");
                            Console.WriteLine($"📱 [ALERTA PROACTIVA MUNDIAL DE AGENDAMIENTO WHATSAPP - 24H ANTES]");
                            Console.WriteLine($"Celular Cliente: {cita.Cliente.telefono} | Correo: {cita.Cliente.email}");
                            Console.WriteLine($"Hola {cita.Cliente.nombre}, te recordamos tu cita de mañana {cita.Fecha:dd/MM/yyyy} a las {cita.Hora:hh\\:mm}.");
                            // 🚩 REPARADO NATIIVAMENTE: Se corrigió de nombre_comercial a NombreComercial respetando tu propiedad real de BD
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
                        
                        modificarAlerta = true;
                    }
                }
                catch (Exception exInner)
                {
                    _logger.LogError($"⚠️ [Turnify Background] Error procesando notificación individual para la cita {cita.Id}: {exInner.Message}");
                }
            }

            if (modificarAlerta)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("🎉 [Turnify Background] Despacho masivo de recordatorios de 24 horas completado con éxito.");
            }
        }
    }
}