using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using System.Runtime.InteropServices;

namespace Turnify.Api.Services
{
    public class CitaService : ICitaService
    {
        private readonly TurnifyDbContext _context;

        public CitaService(TurnifyDbContext context)
        {
            _context = context;
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

        // --- 📊 1. AGENDA POR RANGO ---
        public async Task<IEnumerable<object>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin)
        {
            // 🛡️ Blindaje inicial: Evitar consultas con IDs vacíos
            if (userId == Guid.Empty) return Enumerable.Empty<object>();

            var fechaInicioStr = inicio.Date;
            var fechaFinLimite = fin.Date.AddDays(1);

            return await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Where(c => c.ProveedorId == userId && 
                            c.Fecha >= fechaInicioStr && 
                            c.Fecha < fechaFinLimite && 
                            c.Estado != "cancelada")
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .Select(c => new {
                    c.Id,
                    Fecha = c.Fecha.ToString("yyyy-MM-dd"),
                    Hora = c.Hora.ToString(@"hh\:mm"),
                    ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                    c.Estado,
                    Precio = c.PrecioPactado,
                    Duracion = c.DuracionPactadaMin,
                    c.Observaciones,
                    c.Modalidad,
                    c.MetodoRegistro,
                    c.Direccion
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<object>> GetAgendaHoyAsync(Guid userId)
        {
            var hoyBogota = GetBogotaTime().Date;
            return await GetCitasRangoAsync(userId, hoyBogota, hoyBogota);
        }

        // --- 📝 3. AGENDAR CITA (Transaccional y Blindada) ---
        public async Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto)
        {
            // 🛡️ Validación de integridad de entrada
            if (dto.ClienteId == Guid.Empty || dto.ServicioId == Guid.Empty)
                return (false, "Los identificadores de cliente o servicio no pueden estar vacíos.", (Guid?)null);

            var cliente = await _context.clientes.FindAsync(dto.ClienteId);
            if (cliente == null) return (false, "El cliente especificado no existe.", (Guid?)null);

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

            var inicioNueva = dto.Hora;
            var finNueva = inicioNueva.Add(TimeSpan.FromMinutes(servicio.DuracionMinutos));

            if (inicioNueva < horario.HoraApertura || finNueva > horario.HoraCierre)
                return (false, $"Fuera de horario: Abre {horario.HoraApertura} - Cierra {horario.HoraCierre}.", (Guid?)null);

            // 🛡️ [FIX] Estrategia de ejecución para SQL Server (Indispensable para transacciones con reintentos)
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<(bool Success, string Message, Guid? CitaId)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var citasExistentes = await _context.citas
                        .Where(c => c.ProveedorId == proveedorId && c.Fecha.Date == dto.Fecha.Date && c.Estado != "cancelada")
                        .ToListAsync();

                    var yaExisteCita = citasExistentes.Any(c => 
                        inicioNueva < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < finNueva
                    );

                    if (yaExisteCita) 
                        return (false, "Este horario ya fue ocupado mientras realizabas la solicitud.", (Guid?)null);

                    var nuevaCita = new Citas
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = cliente.id,
                        ProveedorId = proveedorId,    
                        ServicioId = servicio.Id,
                        Fecha = dto.Fecha.Date,
                        Hora = inicioNueva,
                        Modalidad = dto.Modalidad ?? "local",
                        Estado = "pendiente",
                        PrecioPactado = servicio.Precio + (dto.CostoDomicilio >= 0 ? dto.CostoDomicilio : 0), 
                        DuracionPactadaMin = servicio.DuracionMinutos,
                        FechaCreacion = DateTime.UtcNow,
                        Observaciones = dto.Observaciones,
                        Direccion = dto.Direccion,
                        MetodoRegistro = dto.MetodoRegistro ?? "Web",
                        Latitud = dto.Latitud,
                        Longitud = dto.Longitud,
                        CostoDomicilio = dto.CostoDomicilio
                    };

                    _context.citas.Add(nuevaCita);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync(); 

                    return (true, "¡Cita agendada con éxito!", (Guid?)nuevaCita.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(); 
                    Console.WriteLine($"❌ [Error-Fatal] Al agendar cita: {ex.Message}");
                    return (false, "Error interno al procesar la cita. Intenta de nuevo.", (Guid?)null);
                }
            });
        }

        // --- 🕒 4. DISPONIBILIDAD (MOTOR DE BÚSQUEDA BLINDADO) ---
        public async Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha)
        {
            var ahoraBogota = GetBogotaTime();
            
            Console.WriteLine($"🔍 [Lupe-Debug] Consultando: Prov {proveedorId} | Serv {servicioId} | Fecha {fecha:yyyy-MM-dd}");

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

            // 🛡️ Validación extra para asegurar que el proveedorId sea válido
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
            var duracionCita = TimeSpan.FromMinutes(servicio.DuracionMinutos);
            var intervalo = TimeSpan.FromMinutes(30); 

            TimeSpan limiteHoraActual = fecha.Date == ahoraBogota.Date ? ahoraBogota.TimeOfDay : TimeSpan.Zero;

            while (tiempoActual + duracionCita <= horario.HoraCierre)
            {
                if (tiempoActual > limiteHoraActual) 
                {
                    bool ocupado = citasOcupadas.Any(c => 
                        tiempoActual < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < tiempoActual + duracionCita
                    );
                    
                    if (!ocupado) slotsDisponibles.Add(tiempoActual);
                }
                tiempoActual = tiempoActual.Add(intervalo);
            }

            Console.WriteLine($"✅ Slots encontrados: {slotsDisponibles.Count}");
            return slotsDisponibles;
        }

        public async Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha)
        {
            return await GetCitasRangoAsync(proveedorId, fecha, fecha);
        }

        public async Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado)
        {
            var estadosValidos = new[] { "pendiente", "confirmada", "completada", "cancelada", "ausente" };
            nuevoEstado = nuevoEstado.ToLower();
            if (!estadosValidos.Contains(nuevoEstado)) return (false, "Estado no válido.");

            var cita = await _context.citas.FindAsync(id);
            if (cita == null) return (false, "La cita no existe.");

            // 🛡️ Blindaje contra actualizaciones de estado a citas ya canceladas/completadas si fuera necesario
            cita.Estado = nuevoEstado;
            await _context.SaveChangesAsync();
            return (true, $"Cita actualizada a: {nuevoEstado}");
        }

        public async Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId)
        {
            if (clienteId == Guid.Empty) return Enumerable.Empty<object>();

            return await _context.citas.AsNoTracking()
                .Include(c => c.Servicio)
                .Where(c => c.ClienteId == clienteId)
                .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Hora)
                .Select(c => new {
                    c.Id, c.Fecha, c.Hora,
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no especificado",
                    c.Estado, c.PrecioPactado, c.Observaciones,
                    c.Modalidad 
                }).ToListAsync();
        }
    }
}