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

        // 🚩 MÉTODO PRIVADO: Obtener hora actual de Bogotá
        private DateTime GetBogotaTime()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
            var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
        }

        // --- 📊 1. AGENDA POR RANGO (Blindada contra filtraciones de fechas) ---
        public async Task<IEnumerable<object>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin)
        {
            // 🛡️ BLINDAJE 1: Validación de seguridad
            if (userId == Guid.Empty) return Enumerable.Empty<object>();

            // 🛡️ BLINDAJE 2: Normalización de rangos para evitar el bug de "Abril en Junio"
            // Forzamos el inicio al primer segundo del día y el fin al último segundo del día.
            var fechaInicioStr = inicio.Date;
            var fechaFinLimite = fin.Date.AddDays(1);

            return await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Where(c => c.ProveedorId == userId && 
                            c.Fecha >= fechaInicioStr && 
                            c.Fecha < fechaFinLimite && // 🚩 Rango estricto: menor que el día siguiente
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

        // --- 📝 3. AGENDAR CITA (EVOLUCIONADA PARA QR & DOMICILIOS) ---
        public async Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto)
        {
            var cliente = await _context.clientes.FindAsync(dto.ClienteId);
            if (cliente == null) return (false, "El cliente especificado no existe.", null);

            var servicio = await _context.servicios.FindAsync(dto.ServicioId);
            if (servicio == null) return (false, "Servicio no encontrado.", null);

            // 🛡️ AUDITORÍA: Validación de Domicilio
            if (dto.Modalidad.ToLower() == "domicilio" && string.IsNullOrWhiteSpace(dto.Direccion))
                return (false, "La dirección es obligatoria para servicios a domicilio.", null);

            var ahoraBogota = GetBogotaTime();
            var fechaHoraCita = dto.Fecha.Date.Add(dto.Hora);
            
            if (fechaHoraCita < ahoraBogota)
                return (false, "No puedes agendar una cita en el pasado.", null);

            var proveedorId = servicio.ProveedorId.GetValueOrDefault();
            
            if (proveedorId == Guid.Empty)
                return (false, "Este servicio no tiene un proveedor asignado.", null);

            int diaDeLaSemana = (int)dto.Fecha.DayOfWeek;

            var horario = await _context.horarios_atencion
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ProveedorId == proveedorId && h.DiaSemana == diaDeLaSemana);

            if (horario == null) return (false, "El proveedor no trabaja este día.", null);

            var inicioNueva = dto.Hora;
            var finNueva = inicioNueva.Add(TimeSpan.FromMinutes(servicio.DuracionMinutos));

            if (inicioNueva < horario.HoraApertura || finNueva > horario.HoraCierre)
                return (false, $"Fuera de rango de atención ({horario.HoraApertura} - {horario.HoraCierre}).", null);

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
                PrecioPactado = servicio.Precio + dto.CostoDomicilio,
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

        public async Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha)
        {
            // 🚩 Llamamos al método de rango con la misma fecha para asegurar consistencia
            return await GetCitasRangoAsync(proveedorId, fecha, fecha);
        }

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

        public async Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId)
        {
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