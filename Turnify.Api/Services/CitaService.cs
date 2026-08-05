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
        private readonly IEmailService _emailService; // 🚀 Canal de correo electrónico inyectado
        private readonly IWhatsAppService _whatsappService; // 🚀 Canal de WhatsApp Bot inyectado

        public CitaService(TurnifyDbContext context, IEmailService emailService, IWhatsAppService whatsappService)
        {
            _context = context;
            _emailService = emailService;
            _whatsappService = whatsappService;
        }

        // 🚩 MÉTODO PRIVADO: Convertido a DateTimeOffset para eliminar el Timezone Drift global de Docker
        private DateTimeOffset GetBogotaTime()
        {
            try 
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, bogotaZone);
            }
            catch 
            {
                // Backup manual UTC-5 con soporte de Offset si falla el proveedor de zona horaria
                return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5));
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

            // 🛡️ BLINDAJE DEFENSIVO Si las fechas llegan sin inicializar (default o MinValue) desde el login
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

            // 🌐 RECONCILIACIÓN HORARIA: Traducimos los filtros locales a DateTimeOffset universales
            var fechaInicioOffset = new DateTimeOffset(fechaInicioStr, GetBogotaTime().Offset);
            var fechaFinLimiteOffset = new DateTimeOffset(fechaFinLimite, GetBogotaTime().Offset);

            return await _context.citas
                .AsNoTracking()
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Proveedor) // 🚩 INYECTADO: Inclusión del Proveedor para jalar el Nombre Comercial
                .Include(c => c.Empleado)  // 🚀 HU 001: Incluimos el empleado
                .Include(c => c.Estacion)  // 🚀 HU 001: Incluimos la estación
                // 🚩 AJUSTE DE IDENTIDAD: Buscamos por ProveedorId o ClienteId (validando contra usuario_id también)
                .Where(c => (c.ProveedorId == userId || c.ClienteId == userId || (c.Cliente != null && c.Cliente.usuario_id != null && c.Cliente.usuario_id == userId) || c.EmpleadoId == userId) && 
                            c.Fecha >= fechaInicioOffset && 
                            c.Fecha < fechaFinLimiteOffset && 
                            c.Estado != "cancelada")
                .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
                .Select(c => new CitaResponseDto {
                    Id = c.Id,
                    Fecha = c.Fecha.DateTime, // 🌐 Sincronizado para devolver DateTime plano al DTO original
                    Hora = c.Hora,
                    ClienteNombre = c.Cliente != null ? c.Cliente.nombre : "Cliente no registrado",
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                    
                    // 🚩 NUEVO: Mapeo exacto del nombre del establecimiento/proveedor libre de nulos
                    ProveedorNombre = c.Proveedor != null ? (!string.IsNullOrEmpty(c.Proveedor.NombreComercial) ? c.Proveedor.NombreComercial : "Establecimiento Turnify") : "Sin Proveedor",
                    
                    // 🚀 HU 001 / HOTFIX: Proyección de datos Staff libre de "Sin asignar"
                    EmpleadoAsignado = c.Empleado != null 
                        ? c.Empleado.Nombre 
                        : (c.Proveedor != null && !string.IsNullOrEmpty(c.Proveedor.NombreComercial) ? c.Proveedor.NombreComercial : "Especialista Asignado"),
                    EstacionAsignada = c.Estacion != null ? c.Estacion.Nombre : "Local",

                    // 🟢 HU-22: Bandera para identificar si la cita corresponde a un profesional independiente
                    EsIndependiente = c.Proveedor != null && c.Proveedor.EsIndependiente,

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

        // --- 📊 1.1 ESTADÍSTICAS PARA GRÁFICA DE TORTA ---
        public async Task<object> GetEstadisticasTortaAsync(Guid proveedorId)
        {
            var citas = await _context.citas
                .AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId)
                .ToListAsync();

            int total = citas.Count;
            if (total == 0) return new { completadas = 0, pendientes = 0, canceladas = 0, total = 0 };

            var completadas = citas.Count(c => c.Estado == "completada" || c.Estado == "finalizada");
            var pendientes = citas.Count(c => c.Estado == "pendiente" || c.Estado == "en_proceso");
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

            // 🛡️ REGLA DE NEGOCIO: Expiración de 3 meses (90 días) - Solo aplica si la fecha de creación fue registrada
            if (cliente.fecha_creacion != default && cliente.fecha_creacion != DateTime.MinValue)
            {
                var diasDesdeCreacion = (GetBogotaTime().DateTime - cliente.fecha_creacion).TotalDays;
                if (diasDesdeCreacion > 90)
                {
                    return (false, "Cuenta de cliente expirada (máximo 3 meses). Favor actualizar perfil.", (Guid?)null);
                }
            }

            var servicio = await _context.servicios.FindAsync(dto.ServicioId);
            if (servicio == null) return (false, "Servicio no encontrado.", (Guid?)null);

            if (dto.Modalidad.ToLower() == "domicilio" && string.IsNullOrWhiteSpace(dto.Direccion))
                return (false, "La dirección es obligatoria para servicios a domicilio.", (Guid?)null);

            var ahoraBogota = GetBogotaTime();
            
            // 🌐 RECONCILIACIÓN HORARIA MUNDIAL: 
            var fechaHoraCitaOffset = new DateTimeOffset(dto.Fecha.Date.Add(dto.Hora), ahoraBogota.Offset);
            
            if (fechaHoraCitaOffset < ahoraBogota)
                return (false, $"No puedes agendar en el pasado. (Actual en Bog: {ahoraBogota:HH:mm})", (Guid?)null);

            var proveedorId = dto.ProveedorId != Guid.Empty ? dto.ProveedorId : servicio.ProveedorId.GetValueOrDefault();
            
            if (proveedorId == Guid.Empty)
                return (false, "Este servicio no tiene un proveedor asignado.", (Guid?)null);

            // 🎯 CONSULTA DE MODALIDAD DEL PROVEEDOR (HU-22 / CA1 / CA4)
            var proveedorObj = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proveedorId);
            bool esIndependiente = proveedorObj != null && proveedorObj.EsIndependiente;

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

            // 🚨 RESOLUCIÓN BLINDADA DE EMPLEADO Y ESTACIÓN (CA4 / HU-22)
            Guid? empleadoFinalId = dto.EmpleadoId;
            Guid? estacionFinalId = dto.EstacionId;

            if (esIndependiente)
            {
                // 🛡️ BLINDAJE CA4: Si el proveedor es independiente, forzamos EmpleadoId y EstacionId a null 
                // para evitar violaciones de clave foránea en la base de datos (FK_Citas_Empleados)
                empleadoFinalId = null;
                estacionFinalId = null;
            }
            else
            {
                // Si el cliente no seleccionó un profesional preferido en un salón/establecimiento
                if (!empleadoFinalId.HasValue || empleadoFinalId == Guid.Empty)
                {
                    // 1. Busca en la tabla de empleados activos pertenecientes a este proveedor
                    var primerEmpleado = await _context.empleados
                        .AsNoTracking()
                        .Where(e => e.ProveedorId == proveedorId && e.Activo)
                        .Select(e => (Guid?)e.Id)
                        .FirstOrDefaultAsync();

                    if (primerEmpleado.HasValue && primerEmpleado != Guid.Empty)
                    {
                        empleadoFinalId = primerEmpleado;
                    }
                    else
                    {
                        // 2. Busca en usuarios por rol de Staff o ProveedorDependiente
                        var primerUsuario = await _context.usuarios
                            .AsNoTracking()
                            .Where(u => u.activo == true && (u.Rol != null && (u.Rol.nombre == Roles.RoleNames.Staff || u.Rol.nombre == Roles.RoleNames.ProveedorDependiente)))
                            .Select(u => (Guid?)u.id)
                            .FirstOrDefaultAsync();

                        empleadoFinalId = primerUsuario; // Si no encuentra, se mantiene null de forma segura
                    }
                }

                // Fallback para Estación de trabajo si no fue especificada
                if (!estacionFinalId.HasValue || estacionFinalId == Guid.Empty)
                {
                    estacionFinalId = await _context.estaciones_trabajo
                        .AsNoTracking()
                        .Where(e => e.ProveedorId == proveedorId && e.Activo)
                        .Select(e => (Guid?)e.Id)
                        .FirstOrDefaultAsync();
                }
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<(bool Success, string Message, Guid? CitaId)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var targetDateOffset = new DateTimeOffset(dto.Fecha.Date, ahoraBogota.Offset);

                    var citasExistentes = await _context.citas
                        .Where(c => c.ProveedorId == proveedorId && c.Fecha == targetDateOffset && c.Estado != "cancelada")
                        .ToListAsync();

                    // 🚀 HU 001 & CA4: REFINAMIENTO DE OVERBOOKING (Independiente = Monopolio de Horario)
                    var yaExisteCita = citasExistentes.Any(c => 
                        inicioNueva < c.Hora.Add(TimeSpan.FromMinutes(c.DuracionPactadaMin)) && c.Hora < finNueva &&
                        (
                            esIndependiente 
                            ? true 
                            : ((empleadoFinalId.HasValue && c.EmpleadoId == empleadoFinalId) || (estacionFinalId.HasValue && c.EstacionId == estacionFinalId))
                        )
                    );

                    if (yaExisteCita) return (false, "Este bloque de tiempo ya está reservado para ese empleado/silla, o interfiere con otra cita.", (Guid?)null);

                    var nuevaCita = new Citas
                    {
                        Id = Guid.NewGuid(),
                        ClienteId = cliente.id, // 🛡️ Usamos el ID real de la tabla clientes obtenido arriba
                        ProveedorId = proveedorId,    
                        ServicioId = servicio.Id,
                        Fecha = targetDateOffset, // 🌐 Guardado internacional con zona horaria real
                        Hora = inicioNueva,
                        Modalidad = dto.Modalidad ?? "local",
                        Estado = "pendiente",
                        PrecioPactado = servicio.Precio + (dto.CostoDomicilio >= 0 ? dto.CostoDomicilio : 0), 
                        DuracionPactadaMin = servicio.DuracionMinutos,
                        FechaCreacion = DateTimeOffset.UtcNow, // 🌐 Sincronizado con DateTimeOffset universal
                        Observaciones = dto.Observaciones ?? "",
                        Direccion = dto.Direccion,
                        MetodoRegistro = dto.MetodoRegistro ?? "Web",
                        Latitud = dto.Latitud,
                        Longitud = dto.Longitud,
                        CostoDomicilio = dto.CostoDomicilio,
                        
                        // 🚀 HU 001 - MULTI-SILLA & CA4 FIX: Garantiza que EmpleadoId sea null para independientes o id válido
                        EmpleadoId = empleadoFinalId,
                        EstacionId = estacionFinalId,

                        // 🛡️ GENERACIÓN DE TOKEN DE CHECK-IN
                        CodigoVerificacion = GenerarTokenCheckIn()
                    };

                    _context.citas.Add(nuevaCita);
                    await _context.SaveChangesAsync();

                    // 🧠 Mapeamos el nombre del establecimiento comercial antes de cerrar la conexión para el email
                    var establecimientoNombre = "Establecimiento Turnify";
                    var provComercial = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proveedorId);
                    if (provComercial != null)
                    {
                        establecimientoNombre = !string.IsNullOrEmpty(provComercial.NombreComercial) ? provComercial.NombreComercial : "Establecimiento Turnify";
                    }

                    await transaction.CommitAsync(); 

                    // 🚀 DISPARO ASÍNCRONO DE NOTIFICACIONES ELECTRÓNICAS
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 1. Envío automático de correo con la plantilla HTML responsiva
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
                    await transaction.RollbackAsync();
                    Console.WriteLine($"🛡️ [Concurrencia Senior] Choque de escritura detectado al agendar: {ex.Message}");
                    return (false, "Lo sentimos, este cupo de tiempo exacto acaba de ser tomado por otro usuario hace un instante. Por favor, selecciona otro horario.", (Guid?)null);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(); 
                    Console.WriteLine($"❌ [Error-Fatal] Al agendar cita: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"❌ [Error-Fatal Detalle Interno]: {ex.InnerException.Message}");
                    }
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

            // 🌐 RECONCILIACIÓN EN PARÁMETRO DE QUERY: 
            var fechaConsultaOffset = new DateTimeOffset(fecha.Date, ahoraBogota.Offset);

            var citasOcupadas = await _context.citas.AsNoTracking()
                .Where(c => c.ProveedorId == provIdReal && c.Fecha == fechaConsultaOffset && c.Estado != "cancelada")
                .ToListAsync();

            var slotsDisponibles = new List<TimeSpan>();
            var tiempoActual = horario.HoraApertura;
            var duracionSolicitada = TimeSpan.FromMinutes(servicio.DuracionMinutos);
            var intervalo = TimeSpan.FromMinutes(30); 

            TimeSpan limiteHoraActual = fecha.Date == ahoraBogota.Date ? ahoraBogota.TimeOfDay : TimeSpan.Zero;

            // 🚀 HU 001: Lógica Básica de Aforo
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

        // 🛡️ Confirmar Asistencia vía Token (Check-in)
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
                return (false, "La cita está siendo modificada por otro usuario o proceso en este momento de forma paralela.");
            }
        }

        public async Task<IEnumerable<CitaResponseDto>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha)
        {
            return await GetCitasRangoAsync(proveedorId, fecha, fecha);
        }

        public async Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado)
        {
            if (string.IsNullOrWhiteSpace(nuevoEstado)) 
                return (false, "El estado no puede estar vacío.");

            // 🚀 FIX CRÍTICO: Inclusión de estados del ciclo de vida ('en_proceso', 'en proceso', 'finalizada', etc.)
            var estadosValidos = new[] { 
                "pendiente", "confirmada", "completada", "completado", 
                "cancelada", "ausente", "en_proceso", "en proceso", 
                "finalizada", "finalizad" 
            };
            
            nuevoEstado = nuevoEstado.ToLower().Trim();

            if (!estadosValidos.Contains(nuevoEstado)) 
                return (false, "Estado no válido.");

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
                .Include(c => c.Proveedor) // 🚩 INYECTADO: Inclusión del Proveedor para el Historial del Cliente
                .Include(c => c.Empleado)  // 🚀 HU 001: Incluir el Empleado
                .Include(c => c.Estacion)  // 🚀 HU 001: Incluir la Estación
                .Where(c => c.ClienteId == clienteId || (c.Cliente != null && c.Cliente.usuario_id != null && c.Cliente.usuario_id == clienteId))
                .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Hora)
                .Select(c => new CitaResponseDto {
                    Id = c.Id,
                    Fecha = c.Fecha.DateTime, // 🌐 Sincronizado para devolver DateTime plano al DTO original
                    Hora = c.Hora,
                    ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Servicio no especificado",
                    
                    // 🚩 NUEVO: Mapeo exacto del nombre comercial de la barbería/establecimiento para el historial
                    ProveedorNombre = c.Proveedor != null ? (!string.IsNullOrEmpty(c.Proveedor.NombreComercial) ? c.Proveedor.NombreComercial : "Establecimiento Turnify") : "Sin Proveedor",
                    
                    // 🚀 HU 001 / HOTFIX: Información del barbero y la silla libre de "Sin asignar"
                    EmpleadoAsignado = c.Empleado != null 
                        ? c.Empleado.Nombre 
                        : (c.Proveedor != null && !string.IsNullOrEmpty(c.Proveedor.NombreComercial) ? c.Proveedor.NombreComercial : "Especialista Asignado"),
                    EstacionAsignada = c.Estacion != null ? c.Estacion.Nombre : "Local",

                    // 🟢 HU-22: Bandera para identificar si la cita corresponde a un profesional independiente
                    EsIndependiente = c.Proveedor != null && c.Proveedor.EsIndependiente,

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