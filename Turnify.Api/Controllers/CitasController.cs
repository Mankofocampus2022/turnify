using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Turnify.Api.Controllers
{
    // 🚩 DTOs LOCALES: Blindados con soporte multicampo para 'nuevoEstado' y 'estado'
    public class EstadoUpdateDto 
    { 
        [JsonPropertyName("nuevoEstado")]
        public string NuevoEstado { get; set; } = string.Empty; 

        [JsonPropertyName("estado")]
        public string? EstadoAlias 
        { 
            set 
            { 
                if (string.IsNullOrEmpty(NuevoEstado) && !string.IsNullOrEmpty(value)) 
                    NuevoEstado = value; 
            } 
        }
    }

    public class CheckInDto
    {
        public Guid CitaId { get; set; }
        public string Token { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class CitasController : ControllerBase
    {
        private readonly ICitaService _citaService;
        private readonly IDashboardService _dashboardService;
        private readonly TurnifyDbContext _context; // 🛡️ Inyección de base de datos para reparaciones en caliente
        
        public CitasController(ICitaService citaService, IDashboardService dashboardService, TurnifyDbContext context) 
        {
            _citaService = citaService;
            _dashboardService = dashboardService;
            _context = context;
        }

        // 🚩 MÉTODO PRIVADO MODIFICADO PARA SOPORTE INTERNACIONAL:
        // Obtiene el DateTime exacto de la zona horaria del comercio de forma agnóstica al sistema operativo o nube
        private DateTime GetBogotaToday()
        {
            try 
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var targetZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                
                return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, targetZone).Date;
            }
            catch 
            {
                // Fallback dinámico internacional utilizando el desfase estándar
                return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Date;
            }
        }

        // --- 📅 1. AGENDA DE HOY (Filtro Estricto Bogota Time & Isolation HU-12) ---
        [HttpGet("hoy")]
        public async Task<IActionResult> GetCitasHoy()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            if (!Guid.TryParse(userIdClaim, out Guid userId))
                return BadRequest(new { message = "Identificador de usuario malformado." });

            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            return Ok(agenda);
        }

        // --- 📊 2. CITAS POR RANGO (Blindado contra fechas nulas y desbordamiento) ---
        [HttpGet("rango")]
        public async Task<IActionResult> GetCitasRango([FromQuery] DateTime? inicio, [FromQuery] DateTime? fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

            var hoyBogota = GetBogotaToday();
            var fechaInicio = inicio ?? hoyBogota;
            var fechaFin = fin ?? hoyBogota;

            if (fechaFin < fechaInicio) 
                return BadRequest(new { message = "La fecha final no puede ser anterior a la inicial." });

            if (!Guid.TryParse(userIdClaim, out Guid userId))
                return BadRequest(new { message = "Identidad de usuario no válida." });

            var agenda = await _citaService.GetCitasRangoAsync(userId, fechaInicio, fechaFin);
            return Ok(agenda);
        }

        // 📈 --- 3. ANALÍTICA AVANZADA (Aislamiento de métricas de negocio HU-12) ---
        [HttpGet("analitica-avanzada")]
        public async Task<IActionResult> GetAnaliticaAvanzada([FromQuery] Guid proveedorId, [FromQuery] string periodo = "mes", [FromQuery] DateTime? fecha = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var esIndependienteClaim = User.FindFirst("EsIndependiente")?.Value;
            bool isIndependiente = bool.TryParse(esIndependienteClaim, out var ind) && ind;

            // 🛡️ HU-12: Un proveedor dependiente no debe consultar métricas globales del local
            if (User.IsInRole(Roles.RoleNames.ProveedorDependiente))
            {
                var miProveedorIdClaim = User.FindFirst("ProveedorId")?.Value;
                if (miProveedorIdClaim != proveedorId.ToString())
                {
                    return Forbid();
                }
            }

            bool esDuenio = !string.IsNullOrEmpty(userIdClaim) && userIdClaim.Equals(proveedorId.ToString(), StringComparison.OrdinalIgnoreCase);
            
            if (proveedorId != Guid.Empty && !esDuenio && !User.IsInRole(Roles.RoleNames.Administrador) && !User.IsInRole(Roles.RoleNames.Staff) && !isIndependiente)
                return Forbid();

            var analitica = await _dashboardService.GetResumenDiarioAsync(proveedorId, fecha, periodo);
            if (analitica == null) return NotFound(new { message = "No hay datos analíticos para este periodo" });
            return Ok(analitica);
        }

        // 📥 --- 4. EXPORTAR DATA (Control de Acceso estricto) ---
        [HttpGet("exportar/datos")]
        public async Task<IActionResult> GetDatosParaExportar([FromQuery] Guid proveedorId, [FromQuery] DateTime inicio, [FromQuery] DateTime fin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            if (userIdClaim != proveedorId.ToString() && !User.IsInRole(Roles.RoleNames.Administrador) && !User.IsInRole(Roles.RoleNames.Staff)) 
                return Forbid();

            var datos = await _citaService.GetCitasRangoAsync(proveedorId, inicio, fin);
            return Ok(datos);
        }

        // --- 📝 5. AGENDAR CITA (CON PROTECCIÓN DE IDENTIDAD Y BLINDAJE TRY-CATCH) ---
        [HttpPost("agendar")]
        [AllowAnonymous] 
        public async Task<IActionResult> Agendar([FromBody] CitaCreateDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Petición nula." });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try 
            {
                // 🛡️ HU-22 CA4: VALIDACIÓN DE INTEGRIDAD PARA PROFESIONALES INDEPENDIENTES
                var servicioConsulta = await _context.servicios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.ServicioId);
                var targetProveedorId = dto.ProveedorId != Guid.Empty ? dto.ProveedorId : (servicioConsulta?.ProveedorId ?? Guid.Empty);

                if (targetProveedorId != Guid.Empty)
                {
                    var provEntidad = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == targetProveedorId);
                    
                    // 🟢 FIX CA4: Si es profesional independiente, forzamos EmpleadoId y EstacionId a NULL
                    // para evitar violaciones de clave foránea (FK_Citas_Empleados) en la base de datos.
                    if (provEntidad != null && (provEntidad.EsIndependiente || provEntidad.es_independiente))
                    {
                        dto.EmpleadoId = null;
                        dto.EstacionId = null;
                    }
                }

                // 🛡️ BLINDAJE JWT: Mapeo inteligente de ClienteID vs UsuarioID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var clienteIdClaim = User.FindFirst("ClienteId")?.Value;

                if (!string.IsNullOrEmpty(userIdClaim) && User.IsInRole(Roles.RoleNames.Cliente))
                {
                    if (!string.IsNullOrEmpty(clienteIdClaim) && Guid.TryParse(clienteIdClaim, out Guid realClientId))
                    {
                        dto.ClienteId = realClientId;
                        Console.WriteLine($"🔍 [Turnify Auth] Usando ClienteID real: {realClientId}");
                    }
                    else if (Guid.TryParse(userIdClaim, out Guid authUserId))
                    {
                        dto.ClienteId = authUserId;
                        Console.WriteLine($"⚠️ [Turnify Auth] Advertencia: Usando UsuarioID como ClienteID: {authUserId}");
                    }
                }

                // 🚀 RESTRICCIÓN: PROVEEDOR / STAFF NO PUEDEN AGENDARSE A SÍ MISMOS COMO CLIENTES
                if (!string.IsNullOrEmpty(userIdClaim) && !User.IsInRole(Roles.RoleNames.Cliente))
                {
                    if (dto.ClienteId == Guid.Empty && string.IsNullOrEmpty(dto.AnonimoNombre))
                    {
                        return BadRequest(new { message = "Un proveedor o administrador no puede agendar citas para sí mismo. Debe seleccionar un cliente válido de la lista." });
                    }
                }

                // 🚀 GUEST CHECKOUT QR (ANTI-COLISIONAMIENTO DE NULLS DE BD)
                if (dto.ClienteId == Guid.Empty && (!string.IsNullOrEmpty(dto.AnonimoNombre) || !string.IsNullOrEmpty(dto.AnonimoEmail) || !string.IsNullOrEmpty(dto.AnonimoWhatsApp)))
                {
                    var emailTarget = (dto.AnonimoEmail ?? string.Empty).Trim().ToLower();
                    var wppTarget = (dto.AnonimoWhatsApp ?? string.Empty).Trim().ToLower();

                    var clienteExistenteId = Guid.Empty;

                    if (!string.IsNullOrEmpty(emailTarget) || !string.IsNullOrEmpty(wppTarget))
                    {
                        clienteExistenteId = await _context.clientes
                            .AsNoTracking()
                            .Where(c => (!string.IsNullOrEmpty(emailTarget) && c.email != null && c.email.ToLower() == emailTarget) || 
                                        (!string.IsNullOrEmpty(wppTarget) && c.telefono != null && c.telefono == wppTarget))
                            .Select(c => c.id)
                            .FirstOrDefaultAsync();
                    }

                    if (clienteExistenteId != Guid.Empty)
                    {
                        dto.ClienteId = clienteExistenteId;
                        Console.WriteLine($"🔍 [Turnify Guest Checkout] Reutilizando registro de cliente existente libre de nulos: {clienteExistenteId}");
                    }
                    else
                    {
                        Guid nuevoClienteId = Guid.NewGuid();
                        var newCliente = new Clientes
                        {
                            id = nuevoClienteId,
                            nombre = !string.IsNullOrEmpty(dto.AnonimoNombre) ? dto.AnonimoNombre : "Cliente Invitado QR",
                            telefono = !string.IsNullOrEmpty(dto.AnonimoWhatsApp) ? dto.AnonimoWhatsApp : "3000000000",
                            email = !string.IsNullOrEmpty(dto.AnonimoEmail) ? dto.AnonimoEmail : "qr_invitado@turnify.com",
                            activo = true,
                            fecha_creacion = DateTime.UtcNow,
                            usuario_id = null
                        };
                        _context.clientes.Add(newCliente);
                        await _context.SaveChangesAsync();
                        dto.ClienteId = nuevoClienteId;
                        Console.WriteLine($"✅ [Turnify Guest Checkout] Sincronización Exitosa: Perfil creado en caliente para invitado con ID {nuevoClienteId}");
                    }
                }

                // 🚀 AUTO-CREACIÓN EN CALIENTE DEL PERFIL DE CLIENTE
                if (dto.ClienteId != Guid.Empty)
                {
                    var clientExists = await _context.clientes.AnyAsync(c => c.id == dto.ClienteId || c.usuario_id == dto.ClienteId);
                    if (!clientExists)
                    {
                        var userForClient = await _context.usuarios.FindAsync(dto.ClienteId);
                        var newCliente = new Clientes
                        {
                            id = dto.ClienteId,
                            nombre = !string.IsNullOrEmpty(userForClient?.nombre) ? userForClient.nombre : "Cliente Especial QR",
                            telefono = "3000000000",
                            email = !string.IsNullOrEmpty(userForClient?.email) ? userForClient.email : "qr_test@turnify.com",
                            activo = true,
                            fecha_creacion = DateTime.UtcNow,
                            usuario_id = userForClient != null ? dto.ClienteId : null
                        };
                        _context.clientes.Add(newCliente);
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"✅ [Turnify QR Fix] Sincronización Exitosa: Cliente creado en caliente para ID {dto.ClienteId}");
                    }
                }

                var result = await _citaService.AgendarCitaAutomaticaAsync(dto);
                if (!result.Success) 
                    return BadRequest(new { message = result.Message });
                
                return Ok(new { 
                    message = result.Message, 
                    citaId = result.CitaId,
                    modalidad = dto.Modalidad,
                    registro = dto.MetodoRegistro ?? "Web"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Turnify Critical Error] {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ [Turnify Critical Error Detalle]: {ex.InnerException.Message}");
                }
                return StatusCode(500, new { message = "Error interno al agendar: " + ex.Message });
            }
        }

        // 🛡️ --- 🚩 5.1 VALIDAR CHECK-IN (TOKEN) ---
        [HttpPost("validar-checkin")]
        public async Task<IActionResult> ValidarCheckIn([FromBody] CheckInDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de check-in requeridos." });
            if (dto.CitaId == Guid.Empty || string.IsNullOrEmpty(dto.Token))
                return BadRequest(new { message = "Cita ID y Token son obligatorios para el Check-in." });

            var result = await _citaService.ConfirmarAsistenciaAsync(dto.CitaId, dto.Token);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        // --- 🔍 6. CONSULTAR AGENDA POR PROVEEDOR (Para Front Público) ---
        [HttpGet("agenda/{proveedorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAgenda(Guid proveedorId, [FromQuery] DateTime? fecha)
        {
            if (proveedorId == Guid.Empty) return BadRequest(new { message = "ID de proveedor inválido." });
            
            var fechaConsulta = fecha ?? GetBogotaToday();
            var agenda = await _citaService.GetAgendaDiaAsync(proveedorId, fechaConsulta);
            return Ok(agenda);
        }

        // --- 🕒 7. DISPONIBILIDAD (MOTOR OVERBOOKING PRO) ---
        [HttpGet("disponibilidad")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetDisponibilidad([FromQuery] Guid proveedorId, [FromQuery] Guid servicioId, [FromQuery] DateTime? fecha)
        {
            if (proveedorId == Guid.Empty || servicioId == Guid.Empty)
                return BadRequest(new { message = "Proveedor y Servicio son requeridos para calcular el túnel de tiempo." });

            try
            {
                var fechaConsulta = fecha ?? GetBogotaToday();
                var slots = await _citaService.GetDisponibilidadAsync(proveedorId, servicioId, fechaConsulta);
                
                if (slots == null || !slots.Any())
                {
                    Console.WriteLine($"⚠️ [Turnify Check] Sin slots para Proveedor {proveedorId}, Servicio {servicioId}, Fecha {fechaConsulta:yyyy-MM-dd}. Revisar cruce de Horarios y Estaciones de trabajo.");
                    return Ok(new List<string>()); 
                }

                var formattedSlots = slots.Select(s => s.ToString(@"hh\:mm")).ToList();
                return Ok(formattedSlots);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Turnify Dispo Error] Fallo al evaluar túnel de tiempo: {ex.Message}");
                return StatusCode(500, new { message = "Error interno calculando disponibilidad." });
            }
        }

        // --- ⚡ 8. ACTUALIZAR ESTADO (Auditado y Flexibilizado) ---
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> UpdateEstado(Guid id, [FromBody] EstadoUpdateDto dto)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita no válido." });
            
            var estadoTarget = dto?.NuevoEstado?.Trim();

            if (string.IsNullOrEmpty(estadoTarget))
                return BadRequest(new { message = "El nuevo estado es requerido." });

            Console.WriteLine($"🔄 [Turnify PATCH Estado] Actualizando Cita {id} al estado: '{estadoTarget}'");

            var result = await _citaService.UpdateEstadoCitaAsync(id, estadoTarget);
            if (!result.Success) 
                return BadRequest(new { message = result.Message });
            
            return Ok(new { message = result.Message });
        }

        // --- 📜 9. HISTORIAL (Blindaje Habeas Data) ---
        [HttpGet("historial/{clienteId}")]
        public async Task<IActionResult> GetHistorial(Guid clienteId)
        {
            if (clienteId == Guid.Empty) return BadRequest(new { message = "Cliente no identificado." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var clienteIdClaim = User.FindFirst("ClienteId")?.Value;
            
            if (User.IsInRole(Roles.RoleNames.Cliente))
            {
                bool esPropio = userIdClaim == clienteId.ToString() || clienteIdClaim == clienteId.ToString();
                if (!esPropio) return Forbid();
            }

            var historial = await _citaService.GetHistorialClienteAsync(clienteId);
            if (historial == null || !historial.Any())
                return Ok(new { message = "Sin registros previos para este perfil." });
            
            return Ok(historial);
        }

        // --- 📍 10. UBICACIÓN (Blindada y optimizada) ---
        [HttpGet("{id}/ubicacion")]
        public async Task<IActionResult> GetUbicacionDomicilio(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita requerido." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            var cita = agenda.FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "Cita no encontrada o acceso denegado." });
            
            return Ok(new {
                direccion = cita.Direccion,
                modalidad = cita.Modalidad,
                token = cita.CodigoVerificacion
            });
        }

        // --- 💎 11. DETALLE DE CITA (Blindado) ---
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetalleCita(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { message = "ID de cita requerido." });
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            var agenda = await _citaService.GetAgendaHoyAsync(userId);
            var cita = agenda.FirstOrDefault(c => c.Id == id);
            
            if (cita == null) return NotFound(new { message = "No se pudo recuperar la información de la cita." });
            return Ok(cita);
        }

        // --- 👥 12. PROVEEDORES DISPONIBLES (HOTFIX VISIBILIDAD STAFF) ---
        [HttpGet("proveedores-disponibles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProveedoresDisponibles([FromQuery] Guid servicioId, [FromQuery] DateTime? fecha)
        {
            if (servicioId == Guid.Empty)
                return BadRequest(new { message = "El ID del servicio es requerido para filtrar al staff disponible." });

            try
            {
                var fechaConsulta = fecha ?? GetBogotaToday();

                var query = _context.usuarios
                    .AsNoTracking()
                    .Where(u => u.activo == true && u.Rol != null && (u.Rol.nombre == Roles.RoleNames.Staff || u.Rol.nombre == Roles.RoleNames.ProveedorDependiente));

                var proveedores = await query
                    .Select(u => new 
                    {
                        id = u.id,
                        nombre = u.nombre
                    })
                    .ToListAsync();

                if (!proveedores.Any())
                    return Ok(new { message = "No hay staff configurado o visible para este servicio en la fecha indicada.", data = new List<object>() });

                return Ok(new { data = proveedores });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Turnify Staff Error] Bloqueo crítico al listar proveedores: {ex.Message}");
                return StatusCode(500, new { message = "Error interno calculando la visibilidad del staff." });
            }
        }
    }
}