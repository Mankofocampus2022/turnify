using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Turnify.Api.Services
{
    public class CitaService : ICitaService
    {
        private readonly TurnifyDbContext _context;
        private readonly IEmailService _emailService; // 🚀 [NUEVO] Canal de correo electrónico inyectado
        private readonly IWhatsAppService _whatsappService; // 🚀 [NUEVO] Canal de WhatsApp Bot inyectado

        public CitaService(TurnifyDbContext context, IEmailService emailService, IWhatsAppService whatsappService)
        {
            _context = context;
            _emailService = emailService; // 🚀 Sincronizado
            _whatsappService = whatsappService; // 🚀 Sincronizado
        }

        // 🚩 MÉTODO PRIVADO: Obtener hora actual de Bogotá (Blindado contra fallos de TZ)
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
                // Backup manual UTC-5 si falla el proveedor de zona horaria
                return DateTime.UtcNow.AddHours(-5);
            }
        }

        // 🛡️ MÉTODO PRIVADO: Generador de Token de Check-in (6 Caracteres Alfanuméricos)
        private string GenerarTokenCheckIn()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Evitamos O, 0, I, 1 por confusión
            var random = new byte[6];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(random);
            }
            var result = new StringBuilder(6);
            foreach (byte b in random)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }

        // --- 📅 1. AGENDA POR RANGO (Fix de Reportes "Hoy" y Filtro del Panel Principal) ---
        public async Task<IEnumerable<CitaResponseDto>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin)
        {
            // 🛡️ Blindaje inicial: Evitar consultas con IDs vacíos
            if (userId == Guid.Empty) return Enumerable.Empty<CitaResponseDto>();

            // 🛡️ BLINDAJE DEFENSIVO SENIOR: Si las fechas llegan sin inicializar (default o MinValue) desde el login
            // o el panel general, el sistema se auto-recupera reconfigurando el rango para el día de hoy en Bogotá.
            if (inicio == default || fin == default || inicio == DateTime.MinValue || fin == DateTime.MinValue)
            {
                var hoyBogota = GetBogotaTime().Date;
                inicio = hoyBogota;
                fin = hoyBogota;
            }

            // 🚩 FIX CRÍTICO: Aseguramos que si inicio y fin son iguales (Hoy), el rango sea estricto
            var fechaInicioStr = inicio.Date;
            var fechaFinLimite = fin.Date.AddDays(1);

            return await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                // 🚩 AJUSTE DE IDENTIDAD: Buscamos por ProveedorId o ClienteId (validando contra usuario_id también)
                .Where(c => (c.ProveedorId == userId || c.ClienteId == userId || (c.Cliente != null && c.Cliente.usuario_id != null && c.Cliente.usuario_id == userId)) && 
                            c.Fecha >= fechaInicioStr && 
                            c.Fecha < fechaFinLimite && 
                            c.Estado != "cancelada")
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .Select(c => new CitaResponseDto {
                    Id = c.Id,
                    Fecha = c.Fecha,
                    Hora = c.Hora,
                    ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                    
                    // 🛡️ BLINDAJE CONTRA ADVERTENCIAS DE NULABILIDAD (CS8601)
                    Estado = c.Estado ?? "pendiente",
                    PrecioPactado = c.PrecioPactado,
                    DuracionPactadaMin = c.DuracionPactadaMin,
                    Observaciones = c.Observaciones ?? "",
                    Modalidad = c.Modalidad ?? "local",
                    MetodoRegistro = c.MetodoRegistro ?? "Web",
                    
                    Direccion = c.Direccion,
                    // 🛡️ Añadimos el token a la respuesta para que el barbero lo vea en su reporte
                    CodigoVerificacion = c.CodigoVerificacion 
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<CitaResponseDto>> GetAgendaHoyAsync(Guid userId)
        {
            var hoyBogota = GetBogotaTime().Date;
            // Forzamos el rango de un solo día para el Dashboard
            return await GetCitasRangoAsync(userId, hoyBogota, hoyBogota);
        }

        // --- 📊 [NUEVO] 1.1 ESTADÍSTICAS PARA GRÁFICA DE TORTA ---
        public async Task<object> GetEstadisticasTortaAsync(Guid proveedorId)
        {
            var citas = await _context.citas
                .AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId)
                .ToListAsync();

            int total = citas.Count;
            if (total == 0) return new { completadas = 0, pendientes = 0, canceladas = 0, total = 0 };

            var completadas = citas.Count(c => c.Estado == "completada");
            var pendientes = citas.Count(c => c.Estado == "pendiente");
            var canceladas = citas.Count(c => c.Estado == "cancelada");

            return new {
                total,
                porcentajes = new {
                    completadas = Math.Round((double)completadas / total * 100, 2),
                    pendientes = Math.Round((double)pendientes / total * 100, 2),
                    canceladas = Math.Round((double)canceladas / total * 100, 2)
                },
                conteo = new { completadas, pendientes, canceladas }
            };
        }

        // --- 📝 3. AGENDAR CITA (Transaccional, Blindada y con Token) ---
        public async Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto)
        {
            // 🛡️ Validación de integridad de entrada
            if (dto.ClienteId == Guid.Empty || dto.ServicioId == Guid.Empty)
                return (false, "Los identificadores de cliente o servicio no pueden estar vacíos.", (Guid?)null);

            // 🚩 FIX ANTI-NULLS EF CORE: Blindamos la evaluación del predicado aislando los valores nulos de usuario_id
            var cliente = await _context.clientes
                .FirstOrDefaultAsync(c => c.id == dto.ClienteId || (c.usuario_id != null && c.usuario_id == dto.ClienteId));

            if (cliente == null) return (false, "El cliente especificado no existe.", (Guid?)null);

            // 🛡️ REGLA DE NEGOCIO: Expiración de 3 meses (90 días)
            var diasDesdeCreacion = (GetBogotaTime() - cliente.fecha_creacion).TotalDays;
            if (diasDesdeCreacion > 90)
            {
                return (false, "Cuenta de cliente expirada (máximo 3 meses). Favor actualizar perfil.", (Guid?)null);
            }

            var servicio = await _context.servicios.FindAsync(dto.ServicioId);
            if (servicio == null) return (false, "Servicio no encontrado.", (Guid?)null);

            if (dto.Modalidad.ToLower() == "domicilio" && string.IsNullOrWhiteSpace(dto.Direccion))
                return (false, "La dirección es obligatoria para servicios a domicilio.", (Guid?)null);

            var ahoraBogota = GetBogotaTime();
            var fechaHoraCita = dto.Fecha.Date.Add(dto.Hora);
            
            if (fechaHoraCita < ahoraBogota)
                return (false, $"No puedes agendar en el pasado. (Actual en Bog: {ahoraBogota:HH:mm})", (Guid?)null);

            var proveedorId = servicio.ProveedorId.GetValueOrDefault();
            
            if (proveedorId == Guid.Empty)
                return (false, "Este servicio no tiene un proveedor asignado.", (Guid?)null);

            int diaDeLaSemana = (int)dto.Fecha.DayOfWeek;

            var horario = await _context.horarios_atencion
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ProveedorId == proveedorId && h.DiaSemana == diaDeLaSemana);

            if (horario == null) return (false, "El proveedor no atiende en este día de la semana.", (Guid?)null);

            // 🛡️ FIX OVERBOOKING PRO: Validación de bloque completo
            var inicioNueva = dto.Hora;
            var finNueva = inicioNueva.Add(TimeSpan.FromMinutes(servicio.DuracionMinutos));

            if (inicioNueva < horario.HoraApertura || finNueva > horario.HoraCierre)
                return (false, $"Fuera de horario: El servicio de {servicio.DuracionMinutos} min no cabe antes del cierre ({horario.HoraCierre}).", (Guid?)null);

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<(bool Success, string Message, Guid? CitaId)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var citasExistentes = await _context.citas
                        .Where(c => c.ProveedorId == proveedorId && c.Fecha.Date == dto.Fecha.Date && c.Estado != "cancelada")
                        .ToListAsync();

                    // 🛡️ Algoritmo de colisión de bloques (Detecta solapamientos parciales o totales)
                    var yaExisteCita = citasExistentes.Any(c => 
                        inicioNueva < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < finNueva
                    );

                    if (yaExisteCita) return (false, "Este bloque de tiempo ya está reservado o interfiere con otra cita.", (Guid?)null);

                    var nuevaCita = new Citas
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = cliente.id, // 🛡️ Usamos el ID real de la tabla clientes obtenido arriba
                        ProveedorId = proveedorId,    
                        ServicioId = servicio.Id,
                        Fecha = dto.Fecha.Date,
                        Hora = inicioNueva,
                        Modalidad = dto.Modalidad ?? "local",
                        Estado = "pendiente",
                        PrecioPactado = servicio.Precio + (dto.CostoDomicilio >= 0 ? dto.CostoDomicilio : 0), 
                        DuracionPactadaMin = servicio.DuracionMinutos,
                        FechaCreacion = DateTime.UtcNow,
                        Observaciones = dto.Observaciones ?? "", // 🚩 OBSERVACIONES TOTALMENTE RESPETADAS E INTACTAS
                        Direccion = dto.Direccion,
                        MetodoRegistro = dto.MetodoRegistro ?? "Web",
                        Latitud = dto.Latitud,
                        Longitud = dto.Longitud,
                        CostoDomicilio = dto.CostoDomicilio,
                        // 🛡️ GENERACIÓN DE TOKEN DE CHECK-IN
                        CodigoVerificacion = GenerarTokenCheckIn()
                    };

                    _context.citas.Add(nuevaCita);
                    await _context.SaveChangesAsync();

                    // 🧠 INYECTADO SENIOR: Mapeamos el nombre del establecimiento comercial antes de cerrar la conexión para el email
                    var establecimientoNombre = "Establecimiento Turnify";
                    var provComercial = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proveedorId);
                    if (provComercial != null)
                    {
                        establecimientoNombre = !string.IsNullOrEmpty(provComercial.NombreComercial) ? provComercial.NombreComercial : "Establecimiento Turnify";
                    }

                    await transaction.CommitAsync(); 

                    // 🚀 [NUEVO CANAL REACTIVO ASÍNCRONO] - DISPARO DE NOTIFICACIONES ELECTRÓNICAS
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 1. Envío automático de correo con la plantilla HTML responsiva matriculada
                            if (!string.IsNullOrEmpty(cliente.email) && cliente.email.Contains("@"))
                            {
                                await _emailService.EnviarTokenCitaAsync(
                                    cliente.email,
                                    cliente.nombre,
                                    nuevaCita.CodigoVerificacion,
                                    dto.Fecha,
                                    inicioNueva,
                                    servicio.Nombre,
                                    establecimientoNombre
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ [Turnify Background Alerta Error] Falla al despachar tokens: {ex.Message}");
                        }
                    });

                    return (true, $"¡Cita agendada! Código de Check-in: {nuevaCita.CodigoVerificacion}", (Guid?)nuevaCita.Id);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // 🛡️ ¡CAPTURAMOS EL DOBLE AGENDAMIENTO EN EL ACTO!
                    await transaction.RollbackAsync();
                    Console.WriteLine($"🛡️ [Concurrencia Senior] Choque de escritura detectado al agendar: {ex.Message}");
                    return (false, "Lo sentimos, este cupo de tiempo exacto acaba de ser tomado por otro usuario hace un instante. Por favor, selecciona otro horario.", (Guid?)null);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(); 
                    Console.WriteLine($"❌ [Error-Fatal] Al agendar cita: {ex.Message}");
                    return (false, "Error interno al procesar la cita. Intenta de nuevo.", (Guid?)null);
                }
            });
        }

        // --- 🛡️ 4. DISPONIBILIDAD (MOTOR DE BÚSQUEDA BLINDADO - FIX OVERBOOKING PRO) ---
        public async Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha)
        {
            var ahoraBogota = GetBogotaTime();
            
            Console.WriteLine($"🔍 [Lupe-Debug] Consultando Disponibilidad PRO: Prov {proveedorId} | Serv {servicioId} | Fecha {fecha:yyyy-MM-dd}");

            if (fecha.Date < ahoraBogota.Date) 
            {
                Console.WriteLine("⚠️ Bloqueo: Intento de consulta en fecha pasada.");
                return Enumerable.Empty<TimeSpan>();
            }

            var servicio = await _context.servicios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == servicioId);
            if (servicio == null) 
            {
                Console.WriteLine("⚠️ Bloqueo: El servicio no existe.");
                return Enumerable.Empty<TimeSpan>();
            }

            var provIdReal = proveedorId == Guid.Empty ? servicio.ProveedorId.GetValueOrDefault() : proveedorId;

            if (provIdReal == Guid.Empty) return Enumerable.Empty<TimeSpan>();

            int diaSemanaNet = (int)fecha.DayOfWeek; 
            
            var horario = await _context.horarios_atencion.AsNoTracking()
                .FirstOrDefaultAsync(h => h.ProveedorId == provIdReal && h.DiaSemana == diaSemanaNet);

            if (horario == null) 
            {
                Console.WriteLine($"⚠️ Bloqueo: Sin horario en DB para Prov {provIdReal} el día {diaSemanaNet}.");
                return Enumerable.Empty<TimeSpan>();
            }

            var citasOcupadas = await _context.citas.AsNoTracking()
                .Where(c => c.ProveedorId == provIdReal && c.Fecha.Date == fecha.Date && c.Estado != "cancelada")
                .ToListAsync();

            var slotsDisponibles = new List<TimeSpan>();
            var tiempoActual = horario.HoraApertura;
            var duracionSolicitada = TimeSpan.FromMinutes(servicio.DuracionMinutos);
            var intervalo = TimeSpan.FromMinutes(30); 

            TimeSpan limiteHoraActual = fecha.Date == ahoraBogota.Date ? ahoraBogota.TimeOfDay : TimeSpan.Zero;

            while (tiempoActual + duracionSolicitada <= horario.HoraCierre)
            {
                if (tiempoActual > limiteHoraActual) 
                {
                    bool ocupado = citasOcupadas.Any(c => 
                        tiempoActual < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < tiempoActual + duracionSolicitada
                    );
                    
                    if (!ocupado) slotsDisponibles.Add(tiempoActual);
                }
                tiempoActual = tiempoActual.Add(intervalo);
            }

            Console.WriteLine($"✅ Slots Dinámicos Encontrados: {slotsDisponibles.Count} (Servicio de {servicio.DuracionMinutos} min)");
            return slotsDisponibles;
        }

        // 🛡️ NUEVO: Confirmar Asistencia vía Token (Check-in)
        public async Task<(bool Success, string Message)> ConfirmarAsistenciaAsync(Guid citaId, string token)
        {
            try
            {
                var cita = await _context.citas.FindAsync(citaId);
                if (cita == null) return (false, "Cita no encontrada.");

                // 🛑 CANDADO DE ESTADO SENIOR: Si el Worker ya la canceló por inasistencia, el barbero no la puede procesar
                if (cita.Estado == "cancelada")
                    return (false, "No se puede confirmar asistencia. Esta cita ya fue cancelada automáticamente por el sistema debido a inasistencia (pasó el tiempo de gracia).");

                if (cita.CodigoVerificacion != token.ToUpper())
                    return (false, "Token de validación incorrecto.");

                cita.Estado = "completada"; 
                await _context.SaveChangesAsync();
                return (true, "Asistencia confirmada exitosamente.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine($"🛡️ [Concurrencia Senior] Choque al confirmar asistencia: {ex.Message}");
                return (false, "La cita está siendo modificada por otro usuario o proceso en este momento. Por favor, refresca la página.");
            }
        }

        public async Task<IEnumerable<CitaResponseDto>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha)
        {
            return await GetCitasRangoAsync(proveedorId, fecha, fecha);
        }

        public async Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado)
        {
            var estadosValidos = new[] { "pendiente", "confirmada", "completada", "cancelada", "ausente" };
            nuevoEstado = nuevoEstado.ToLower();
            if (!estadosValidos.Contains(nuevoEstado)) return (false, "Estado no válido.");

            try
            {
                var cita = await _context.citas.FindAsync(id);
                if (cita == null) return (false, "La cita no existe.");

                cita.Estado = nuevoEstado;
                await _context.SaveChangesAsync();
                return (true, $"Cita actualizada a: {nuevoEstado}");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine($"🛡️ [Concurrencia Senior] Choque al actualizar estado de la cita: {ex.Message}");
                return (false, "No se pudo cambiar el estado porque la cita fue modificada en paralelo desde otra sesión. Por favor, refresca.");
            }
        }

        public async Task<IEnumerable<CitaResponseDto>> GetHistorialClienteAsync(Guid clienteId)
        {
            if (clienteId == Guid.Empty) return Enumerable.Empty<CitaResponseDto>();

            return await _context.citas.AsNoTracking()
                .Include(c => c.Servicio)
                .Include(c => c.Cliente) 
                .Where(c => c.ClienteId == clienteId || (c.Cliente != null && c.Cliente.usuario_id != null && c.Cliente.usuario_id == clienteId))
                .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Hora)
                .Select(c => new CitaResponseDto {
                    Id = c.Id,
                    Fecha = c.Fecha,
                    Hora = c.Hora,
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no especificado",
                    
                    // 🛡️ BLINDAJE CONTRA ADVERTENCIAS DE NULABILIDAD (CS8601)
                    Estado = c.Estado ?? "pendiente",
                    PrecioPactado = c.PrecioPactado,
                    Observaciones = c.Observaciones ?? "",
                    Modalidad = c.Modalidad ?? "local",
                    MetodoRegistro = c.MetodoRegistro ?? "Web",
                    
                    // 🛡️ REFUERZO: Mapeamos el token para que el cliente lo vea en su pestaña de "Mis Citas"
                    CodigoVerificacion = c.CodigoVerificacion 
                }).ToListAsync();
        }
    }
}