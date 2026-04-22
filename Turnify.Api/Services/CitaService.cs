using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using System.Runtime.InteropServices; // Necesario para detectar el SO y la Zona Horaria

namespace Turnify.Api.Services
{
    public class CitaService : ICitaService
    {
        private readonly TurnifyDbContext _context;

        public CitaService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 🚩 MÉTODO PRIVADO: Obtener hora actual de Bogotá (Blindaje para Docker/Linux/Windows)
        private DateTime GetBogotaTime()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
            var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
        }

        // --- 📊 1. AGENDA POR RANGO ---
        public async Task<IEnumerable<object>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin)
        {
            return await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Where(c => c.ProveedorId == userId && 
                            c.Fecha.Date >= inicio.Date && 
                            c.Fecha.Date <= fin.Date && 
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
                    c.Observaciones
                })
                .ToListAsync();
        }

        // --- 🕒 2. AGENDA DE HOY ---
        public async Task<IEnumerable<object>> GetAgendaHoyAsync(Guid userId)
        {
            var hoyBogota = GetBogotaTime().Date;
            return await GetCitasRangoAsync(userId, hoyBogota, hoyBogota);
        }

        // --- 📝 3. AGENDAR CITA (Blindado) ---
        public async Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto)
        {
            var cliente = await _context.clientes.FindAsync(dto.ClienteId);
            if (cliente == null) return (false, "El cliente especificado no existe.", null);

            var servicio = await _context.servicios.FindAsync(dto.ServicioId);
            if (servicio == null) return (false, "Servicio no encontrado.", null);

            // 🛡️ Blindaje de tiempo: Usamos la hora de Bogotá, no la del servidor local (UTC)
            var ahoraBogota = GetBogotaTime();
            var fechaHoraCita = dto.Fecha.Date.Add(dto.Hora);
            
            if (fechaHoraCita < ahoraBogota)
                return (false, "No puedes agendar una cita en el pasado.", null);

            var proveedorId = servicio.ProveedorId;
            int diaDeLaSemana = (int)dto.Fecha.DayOfWeek;

            var horario = await _context.horarios_atencion
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ProveedorId == proveedorId && h.DiaSemana == diaDeLaSemana);

            if (horario == null) return (false, "El proveedor no trabaja este día.", null);

            var inicioNueva = dto.Hora;
            var finNueva = inicioNueva.Add(TimeSpan.FromMinutes(servicio.DuracionMinutos));

            if (inicioNueva < horario.HoraApertura || finNueva > horario.HoraCierre)
                return (false, $"Fuera de rango de atención ({horario.HoraApertura} - {horario.HoraCierre}).", null);

            // Validación de cruces
            var citasExistentes = await _context.citas
                .AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId && c.Fecha.Date == dto.Fecha.Date && c.Estado != "cancelada")
                .ToListAsync();

            var yaExisteCita = citasExistentes.Any(c => 
                inicioNueva < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < finNueva
            );

            if (yaExisteCita) return (false, "Este horario ya está ocupado.", null);

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
                PrecioPactado = servicio.Precio,
                DuracionPactadaMin = servicio.DuracionMinutos,
                FechaCreacion = DateTime.UtcNow,
                Observaciones = dto.Observaciones
            };

            _context.citas.Add(nuevaCita);
            await _context.SaveChangesAsync();

            return (true, "¡Cita agendada con éxito!", nuevaCita.Id);
        }

        // --- 🕒 4. DISPONIBILIDAD ---
        public async Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha)
        {
            var servicio = await _context.servicios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == servicioId);
            if (servicio == null) return Enumerable.Empty<TimeSpan>();

            var horario = await _context.horarios_atencion.AsNoTracking()
                .FirstOrDefaultAsync(h => h.ProveedorId == proveedorId && h.DiaSemana == (int)fecha.DayOfWeek);

            if (horario == null) return Enumerable.Empty<TimeSpan>();

            var citasOcupadas = await _context.citas.AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId && c.Fecha.Date == fecha.Date && c.Estado != "cancelada")
                .ToListAsync();

            var slotsDisponibles = new List<TimeSpan>();
            var tiempoActual = horario.HoraApertura;
            var duracionCita = TimeSpan.FromMinutes(servicio.DuracionMinutos);
            var intervalo = TimeSpan.FromMinutes(30); 

            // 🛡️ Sincronización de hora actual para el día de hoy
            var ahoraBogota = GetBogotaTime();
            TimeSpan limiteHora = fecha.Date == ahoraBogota.Date ? ahoraBogota.TimeOfDay : TimeSpan.Zero;

            while (tiempoActual + duracionCita <= horario.HoraCierre)
            {
                if (tiempoActual > limiteHora) 
                {
                    bool ocupado = citasOcupadas.Any(c => 
                        tiempoActual < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < tiempoActual + duracionCita
                    );
                    if (!ocupado) slotsDisponibles.Add(tiempoActual);
                }
                tiempoActual = tiempoActual.Add(intervalo);
            }
            return slotsDisponibles;
        }

        // --- 📅 5. AGENDA DEL DÍA ---
        public async Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha)
        {
            return await GetCitasRangoAsync(proveedorId, fecha, fecha);
        }

        // --- ⚡ 6. ACTUALIZAR ESTADO ---
        public async Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado)
        {
            var estadosValidos = new[] { "pendiente", "confirmada", "completada", "cancelada", "ausente" };
            nuevoEstado = nuevoEstado.ToLower();
            if (!estadosValidos.Contains(nuevoEstado)) return (false, "Estado no válido.");

            var cita = await _context.citas.FindAsync(id);
            if (cita == null) return (false, "La cita no existe.");

            cita.Estado = nuevoEstado;
            await _context.SaveChangesAsync();
            return (true, $"Cita actualizada a: {nuevoEstado}");
        }

        // --- 📜 7. HISTORIAL CLIENTE ---
        public async Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId)
        {
            return await _context.citas.AsNoTracking()
                .Include(c => c.Servicio)
                .Where(c => c.ClienteId == clienteId)
                .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Hora)
                .Select(c => new {
                    c.Id, c.Fecha, c.Hora,
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no especificado",
                    c.Estado, c.PrecioPactado, c.Observaciones
                }).ToListAsync();
        }
    }
}