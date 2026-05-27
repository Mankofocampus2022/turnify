using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using System.Runtime.InteropServices;

namespace Turnify.Api.Workers
{
    public class CitaCancellationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CitaCancellationWorker> _logger;
        // Correrá cada 10 minutos. Puedes ajustarlo según las necesidades del negocio
        private readonly TimeSpan _periodoEjecucion = TimeSpan.FromMinutes(10); 

        public CitaCancellationWorker(IServiceProvider serviceProvider, ILogger<CitaCancellationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 [Turnify Background] El motor de cancelación automática de citas ha despertado.");

            // Usamos PeriodicTimer (.NET 6+) que es mucho más eficiente que Task.Delay
            using var timer = new PeriodicTimer(_periodoEjecucion);

            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("🔍 [Turnify Background] Ejecutando barrido de citas no asistidas...");
                    await ProcesarCitasVencidasAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ [Turnify Background Error] Error en el ciclo del Worker: {ex.Message}");
                }
            }
        }

        private async Task ProcesarCitasVencidasAsync()
        {
            // 🛡️ Regla de Arquitectura: Al ser un BackgroundService (Singleton), 
            // debemos crear un Scope manual para consumir el DbContext (Scoped).
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TurnifyDbContext>();

            var ahoraBogota = GetBogotaTime();
            var fechaHoy = ahoraBogota.Date;
            var horaActual = ahoraBogota.TimeOfDay;

            // ⏱️ Regla de Negocio: Damos 15 minutos de gracia antes de darla por "No Asistida"
            var horaLimite = horaActual.Subtract(TimeSpan.FromMinutes(15));

            // Buscamos citas pendientes que cumplan:
            // 1. Sean de días anteriores OR (Sean de hoy pero su hora + 15 min de gracia ya pasaron)
            var citasVencidas = await context.citas
                .Where(c => c.Estado == "pendiente" && 
                            (c.Fecha < fechaHoy || (c.Fecha == fechaHoy && c.Hora < horaLimite)))
                .ToListAsync();

            if (!citasVencidas.Any())
            {
                _logger.LogInformation("✅ [Turnify Background] Todo al día. No se encontraron citas vencidas.");
                return;
            }

            _logger.LogWarning($"⚠️ [Turnify Background] Se encontraron {citasVencidas.Count} citas vencidas. Cancelando por inasistencia...");

            foreach (var cita in citasVencidas)
            {
                cita.Estado = "cancelada";
                cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones) 
                    ? "Cancelada automáticamente por el sistema debido a inasistencia." 
                    : $"{cita.Observaciones} | Cancelada automáticamente por el sistema debido a inasistencia.";
                
                // 🔔 ESPACIO LISTO PARA WHATSAPP:
                // Aquí es donde en el futuro inyectaremos el servicio de mensajería:
                // _whatsappService.EnviarAlertaInasistencia(cita.ClienteId, cita.Fecha, cita.Hora);
            }

            // Guardamos todos los cambios masivos de un solo golpe con el row_version activo
            await context.SaveChangesAsync();
            _logger.LogInformation("🎉 [Turnify Background] Barrido completado con éxito y estados actualizados.");
        }

        // 🚩 Auxiliar para sincronizar la hora exacta de Bogotá
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
                return DateTime.UtcNow.AddHours(-5);
            }
        }
    }
}