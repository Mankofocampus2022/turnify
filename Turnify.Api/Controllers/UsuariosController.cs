using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; 
using Turnify.Api.Interfaces;
using Turnify.Api.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.Logging; 

namespace Turnify.Api.Controllers
{
    [Route("api/Usuarios")] 
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IConfiguration _config;
        private readonly TurnifyDbContext _context;
        private readonly ILogger<UsuariosController> _logger; 

        public UsuariosController(
            IUsuarioService usuarioService, 
            IConfiguration config, 
            TurnifyDbContext context,
            ILogger<UsuariosController> logger)
        {
            _usuarioService = usuarioService;
            _config = config;
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // 1. OBTENER TODOS LOS USUARIOS (Directorio Multi-tenant Refactorizado)
        // ============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("--- 🔍 GET: Listando todos los usuarios según jerarquía ---");

            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

            var userId = Guid.Parse(usuarioIdClaim);
            var rolClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            string staffRoleName = Roles.RoleNames.Staff;
            
            var isAdmin = rolClaim != null && (rolClaim.ToLower().Contains("admin") || rolClaim.ToLower().Contains("super"));
            var isStaff = rolClaim != null && rolClaim.Equals(staffRoleName, StringComparison.OrdinalIgnoreCase);

            IQueryable<Usuarios> query = _context.usuarios.Include(u => u.Rol);

            // Si es Administrador de la plataforma global ve todo el ecosistema
            if (isAdmin)
            {
                // No se aplica filtro restrictivo
            }
            // 🚀 Si es Dueño del Local (Staff), filtramos para que vea su personal y sus clientes web
            else if (isStaff)
            {
                var proveedor = await _context.proveedores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UsuarioId == userId);

                if (proveedor != null)
                {
                    // 1. Capturamos los UsuarioId de sus Empleados/Colaboradores contratados
                    var empleadosUserIds = await _context.empleados
                        .AsNoTracking()
                        .Where(e => e.ProveedorId == proveedor.Id && e.UsuarioId != null)
                        .Select(e => e.UsuarioId!.Value)
                        .ToListAsync();

                    // Capturar también proveedores dependientes asignados por StaffId
                    var proveedoresDependientesUserIds = await _context.proveedores
                        .AsNoTracking()
                        .Where(p => p.StaffId == proveedor.Id && p.UsuarioId != null)
                        .Select(p => p.UsuarioId!.Value)
                        .ToListAsync();

                    // 2. Capturamos los usuario_id de los Clientes que agendaron en su búnker
                    var clientesUserIds = await _context.clientes
                        .AsNoTracking()
                        .Where(c => c.usuario_id != null && _context.citas.Any(cita => cita.ProveedorId == proveedor.Id && cita.ClienteId == c.id))
                        .Select(c => c.usuario_id!.Value)
                        .Distinct()
                        .ToListAsync();

                    // Unificamos el listado para el directorio administrativo
                    var totalListIds = empleadosUserIds
                        .Concat(proveedoresDependientesUserIds)
                        .Concat(clientesUserIds)
                        .Distinct()
                        .ToList();

                    // Incluimos al propio dueño en el listado para evitar auto-exclusión
                    totalListIds.Add(userId);

                    query = query.Where(u => totalListIds.Contains(u.id));
                }
                else
                {
                    query = query.Where(u => u.id == userId);
                }
            }
            else // Si es un Barbero/Colaborador (Proveedor) o Cliente, solo ve su propio registro
            {
                query = query.Where(u => u.id == userId);
            }

            var usuarios = await query
                .Select(u => new {
                    u.id,
                    u.nombre,
                    u.email,
                    rol = u.Rol != null ? u.Rol.nombre : "Sin Rol", 
                    u.esta_bloqueado,
                    u.suscripcion_fin,
                    u.rol_id
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // ============================================================
        // 2. LOGIN - REPARACIÓN SINCRO DE IDENTIDAD (Inversión Multi-rol)
        // ============================================================
        [HttpPost("login")]
        [AllowAnonymous] 
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            _logger.LogInformation("--- 📩 Intento de Login Alterno: {Email} ---", dto?.Email ?? "NULO");

            if (dto == null || string.IsNullOrWhiteSpace(dto.Email)) 
                return BadRequest(new { message = "Cuerpo de petición nulo o credenciales vacías." });

            try 
            {
                var result = await _usuarioService.LoginAsync(dto);
                
                if (!result.Success) 
                {
                    _logger.LogWarning("--- ⚠️ Fallo de Auth: {Message} ---", result.Message);
                    return Unauthorized(new { message = result.Message });
                }

                if (result.Data is Usuarios usuarioLogueado)
                {
                    var usuarioConRol = await _context.usuarios
                        .Include(u => u.Rol)
                        .FirstOrDefaultAsync(u => u.id == usuarioLogueado.id);

                    if (usuarioConRol == null) return Unauthorized(new { message = "Error al recuperar perfil." });

                    // AUTO-VINCULACIÓN DE CLIENTE (Alexandra Fix)
                    var cliente = await _context.clientes.FirstOrDefaultAsync(c => c.usuario_id == usuarioConRol.id);
                    
                    if (cliente == null && (usuarioConRol.Rol?.nombre ?? "").Contains("Cliente", StringComparison.OrdinalIgnoreCase))
                    {
                        cliente = new Clientes {
                            id = Guid.NewGuid(),
                            usuario_id = usuarioConRol.id,
                            nombre = usuarioConRol.nombre,
                            email = usuarioConRol.email,
                            telefono = "3110000000",
                            activo = true,
                            fecha_creacion = DateTime.UtcNow
                        };
                        _context.clientes.Add(cliente);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ [Turnify] Cliente creado automáticamente para: {Email}", usuarioConRol.email);
                    }

                    // RESOLUCIÓN DE RELACIONES SEGÚN ROL INVERTIDO (HU-08 a HU-12)
                    Guid? proveedorId = null;
                    Guid? empleadoId = null;
                    bool esIndependiente = false;
                    string? fotoUrl = null;

                    string defaultRoleCliente = Roles.RoleNames.Cliente;
                    string roleStaff = Roles.RoleNames.Staff;
                    string roleProveedor = Roles.RoleNames.Proveedor;
                    string roleProveedorDep = Roles.RoleNames.ProveedorDependiente;

                    var rolNombre = usuarioConRol.Rol?.nombre ?? defaultRoleCliente;

                    // 🔧 FIX: Jerarquía estricta según el Rol real del usuario
                    if (rolNombre.Equals(roleStaff, StringComparison.OrdinalIgnoreCase))
                    {
                        // 1. Si el Rol es STAFF (Dueño/Búnker), SIEMPRE es esIndependiente = false
                        esIndependiente = false;

                        var prov = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                        if (prov != null)
                        {
                            proveedorId = prov.Id;
                            fotoUrl = prov.FotoUrl;
                        }
                    }
                    else if (rolNombre.Equals(roleProveedor, StringComparison.OrdinalIgnoreCase) || 
                             rolNombre.Equals(roleProveedorDep, StringComparison.OrdinalIgnoreCase)) 
                    {
                        // 2. Si es Barbero/Colaborador
                        var emp = await _context.empleados.FirstOrDefaultAsync(e => e.UsuarioId == usuarioConRol.id);
                        if (emp != null)
                        {
                            empleadoId = emp.Id;
                            proveedorId = emp.ProveedorId;
                            fotoUrl = emp.FotoUrl;
                            esIndependiente = false;
                        }
                        else
                        {
                            var prov = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                            if (prov != null)
                            {
                                proveedorId = prov.Id;
                                esIndependiente = prov.EsIndependiente;
                                fotoUrl = prov.FotoUrl;
                            }
                        }
                    }
                    else 
                    {
                        // 3. Demás roles (Clientes / Admins)
                        var prov = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                        if (prov != null)
                        {
                            proveedorId = prov.Id;
                            esIndependiente = prov.EsIndependiente;
                            fotoUrl = prov.FotoUrl;
                        }
                    }

                    // Generamos token inyectando de forma segura los claims mapeados
                    var token = GenerarTokenJWT(usuarioConRol, cliente?.id, proveedorId, empleadoId, esIndependiente, fotoUrl);

                    _logger.LogInformation("--- ✅ Login Exitoso Unificado: {Email} ---", usuarioConRol.email);

                    return Ok(new { 
                        token = token, 
                        user = new { 
                            id = usuarioConRol.id,
                            clienteId = cliente?.id, 
                            proveedorId = proveedorId,
                            empleadoId = empleadoId, 
                            nombre = usuarioConRol.nombre, 
                            email = usuarioConRol.email, 
                            rol = rolNombre,
                            esIndependiente = esIndependiente,
                            fotoUrl = fotoUrl
                        } 
                    });
                }
                return StatusCode(500, new { message = "Error de formato en datos." });
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "--- 🚨 CRASH EN ENDPOINT LOGIN ---");
                return StatusCode(500, new { message = ex.Message }); 
            }
        }

        // ============================================================
        // 3. REGISTRAR - CON CONTROL DE INYECCIÓN (Corregido a Guid)
        // ============================================================
        [HttpPost("registrar")]
        [AllowAnonymous]
        public async Task<IActionResult> Registrar([FromBody] UsuarioRegistroDTO dto)
        {
            _logger.LogInformation("--- 📝 Intento de registro para: {Email} ---", dto?.Email);
            
            if (dto == null) return BadRequest(new { message = "Datos inválidos." });

            // 🔹 FIX CS0029 & CS0019: Uso de Guid y constantes del modelo Roles
            Guid adminRoleGuid = Roles.RoleIds.SuperAdministrador;
            Guid superAdminRoleGuid = Roles.RoleIds.Administrador;

            if (dto.RolId == adminRoleGuid || dto.RolId == superAdminRoleGuid)
            {
                if (!Request.Headers.TryGetValue("X-Admin-Creation-Key", out var tokenCabecera) || 
                    tokenCabecera != "TurnifyAdminSecure2026Key")
                {
                    return Unauthorized(new { message = "Acceso denegado. No posees las llaves de alta." });
                }
            }

            try 
            {
                var result = await _usuarioService.RegistrarAsync(dto);
                if (result.Success)
                    return Ok(new { message = "¡Registro exitoso!", usuarioId = result.UsuarioId });
                
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "--- 🚨 ERROR EN REGISTRO ---");
                return StatusCode(500, new { message = "Error interno del servidor." }); 
            }
        }

        // ============================================================
        // 4. RECUPERACIÓN DE CONTRASEÑA (Intacto)
        // ============================================================
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email == dto.Email);
            if (usuario == null) return BadRequest(new { message = "El correo no existe." });

            usuario.ResetToken = Guid.NewGuid().ToString();
            usuario.ResetTokenExpires = DateTime.UtcNow.AddHours(1); 

            await _context.SaveChangesAsync();
            return Ok(new { message = "Token generado", token = usuario.ResetToken });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de petición nulos." });

            var emailInput = dto.Email?.Trim().ToLower() ?? string.Empty;
            var telefonoInput = new string((dto.Telefono ?? "").Where(char.IsDigit).ToArray()); 
            var tokenInput = dto.Token?.Trim() ?? string.Empty;

            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => 
                u.ResetToken == tokenInput && !string.IsNullOrEmpty(tokenInput) && u.ResetTokenExpires > DateTime.UtcNow);

            if (usuario == null)
            {
                usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email != null && u.email.ToLower() == emailInput);
                if (usuario != null)
                {
                    var esClienteValido = await _context.clientes
                        .AnyAsync(c => c.usuario_id == usuario.id && 
                                       c.email != null && c.email.ToLower() == emailInput &&
                                       c.telefono != null && 
                                       EF.Functions.Like(c.telefono, $"%{telefonoInput}%"));

                    bool matchProveedor = await _context.proveedores
                        .AnyAsync(p => p.UsuarioId == usuario.id && 
                                       p.Email != null && p.Email.ToLower() == emailInput &&
                                       p.Telefono != null && 
                                       EF.Functions.Like(p.Telefono, $"%{telefonoInput}%"));

                    if (!esClienteValido && !matchProveedor) usuario = null; 
                }
            }

            if (usuario == null) 
            {
                return BadRequest(new { message = "Datos de validación incorrectos." });
            }

            try 
            {
                usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                usuario.ResetToken = null;
                usuario.ResetTokenExpires = null;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Contraseña actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "--- ERROR AL GUARDAR PASS ---");
                return StatusCode(500, new { message = "Error al procesar el cambio." });
            }
        }

        // ============================================================
        // 5. GESTIÓN Y ESTADÍSTICAS (Intactas)
        // ============================================================
        [HttpPut("cambiar-estado/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromQuery] bool bloquear)
        {
            var exito = await _usuarioService.CambiarEstadoBloqueoAsync(id, bloquear);
            return exito ? Ok(new { message = "Estado actualizado" }) : NotFound();
        }

        [HttpGet("dashboard-stats")]
        [Authorize] 
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var usuariosCount = await _usuarioService.GetTotalUsuariosActivosAsync();
                var proveedoresCount = await _context.proveedores.CountAsync(); 
                return Ok(new { usuariosCount, proveedoresCount, ingresosMensuales = 0 });
            }
            catch (Exception) { return StatusCode(500, new { message = "Error" }); }
        }

        [HttpPut("renovar/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> RenovarSuscripcion(Guid id, [FromQuery] int meses = 1)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var fechaBase = (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value > DateTime.UtcNow) 
                                ? usuario.suscripcion_fin.Value : DateTime.UtcNow;

            usuario.suscripcion_fin = fechaBase.AddMonths(meses);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción extendida", nuevaFecha = usuario.suscripcion_fin });
        }

        // ============================================================
        // 6. CRUD BÁSICO 
        // ============================================================
        [HttpPut("{id:guid}")] 
        public async Task<IActionResult> Update(Guid id, [FromBody] Usuarios u) 
        { 
            if (id != u.id) return BadRequest(); 
            return await _usuarioService.ActualizarAsync(u) ? Ok() : BadRequest(); 
        }

        [HttpDelete("{id:guid}")] 
        public async Task<IActionResult> Delete(Guid id) 
        { 
            return await _usuarioService.EliminarLogicoAsync(id) ? Ok() : NotFound(); 
        }

        [HttpGet("{id:guid}")] 
        public async Task<IActionResult> GetById(Guid id) 
        { 
            var u = await _usuarioService.GetUsuarioByIdAsync(id); 
            return u == null ? NotFound() : Ok(u); 
        }

        // 🚩 GENERAR TOKEN - COMPATIBILIDAD EXPANDIDA CON CLAIMS DE EMPLEADO, INDEPENDIENTE Y FOTO (HU-12)
        private string GenerarTokenJWT(
            Usuarios usuario, 
            Guid? clienteId = null, 
            Guid? proveedorId = null, 
            Guid? empleadoId = null,
            bool esIndependiente = false,
            string? fotoUrl = null)
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") 
                ?? _config["Jwt:Key"] 
                ?? "Turnify_Secret_Key_2026_Enterprise_Edition_Security_PRO";

            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 16)
            {
                jwtKey = "Turnify_Master_Secret_Key_Enterprise_Secure_2026_Edition_PRO_Security_Crypto_Engine_512_Bits#";
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            string roleCliente = Roles.RoleNames.Cliente;

            var claims = new List<Claim> { 
                new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()), 
                new Claim(ClaimTypes.Name, usuario.nombre ?? ""), 
                new Claim(ClaimTypes.Role, usuario.Rol?.nombre ?? roleCliente),
                new Claim("EsIndependiente", esIndependiente.ToString().ToLower())
            };

            if (clienteId.HasValue) claims.Add(new Claim("ClienteId", clienteId.Value.ToString()));
            if (proveedorId.HasValue) claims.Add(new Claim("ProveedorId", proveedorId.Value.ToString()));
            if (empleadoId.HasValue) claims.Add(new Claim("EmpleadoId", empleadoId.Value.ToString())); 
            if (!string.IsNullOrEmpty(fotoUrl)) claims.Add(new Claim("FotoUrl", fotoUrl));

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "Turnify.Api",
                audience: _config["Jwt:Audience"] ?? "Turnify.App",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(1440),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}