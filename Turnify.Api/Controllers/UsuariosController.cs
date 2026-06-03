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
        // 1. OBTENER TODOS LOS USUARIOS (Multi-tenant - Intacto)
        // ============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("--- 🔍 GET: Listando todos los usuarios ---");

            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

            var userId = Guid.Parse(usuarioIdClaim);
            var rolClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            
            var isAdmin = rolClaim != null && (rolClaim.ToLower().Contains("admin") || rolClaim.ToLower().Contains("super"));

            IQueryable<Usuarios> query = _context.usuarios.Include(u => u.Rol);

            if (!isAdmin)
            {
                var proveedor = await _context.proveedores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

                if (proveedor != null)
                {
                    var userIds = await _context.clientes.AsNoTracking()
                        .Where(c => _context.citas.Any(cita => cita.ProveedorId == proveedor.Id && cita.ClienteId == c.id))
                        .Select(c => c.usuario_id)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(u => userIds.Contains(u.id));
                }
                else
                {
                    query = query.Where(u => u.id == userId);
                }
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
        // 2. LOGIN - REPARACIÓN DE IDENTIDAD PARA CLIENTES (Intacto)
        // ============================================================
        [HttpPost("login")]
        [AllowAnonymous] 
        public async Task<IActionResult> Login([FromBody] Turnify.Api.Models.DTOs.LoginDto dto)
        {
            _logger.LogInformation("--- 📩 Intento de Login: {Email} ---", dto?.Email ?? "NULO");

            if (dto == null) return BadRequest(new { message = "Cuerpo de petición nulo." });

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

                    // 🚩 AUTO-VINCULACIÓN DE CLIENTE (Alexandra Fix - Intacto)
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

                    var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                    
                    // Generamos token pasando todos los IDs para que viajen en los Claims
                    var token = GenerarTokenJWT(usuarioConRol, cliente?.id, proveedor?.Id);

                    _logger.LogInformation("--- ✅ Login Exitoso: {Email} ---", usuarioConRol.email);

                    return Ok(new { 
                        token = token, 
                        user = new { 
                            id = usuarioConRol.id,
                            clienteId = cliente?.id, 
                            proveedorId = proveedor?.Id,
                            nombre = usuarioConRol.nombre, 
                            email = usuarioConRol.email, 
                            rol = usuarioConRol.Rol?.nombre ?? "Usuario"
                        } 
                    });
                }
                return StatusCode(500, new { message = "Error de formato en datos." });
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "--- 🚨 CRASH EN LOGIN ---");
                return StatusCode(500, new { message = ex.Message }); 
            }
        }

        // ============================================================
        // 🛡️ 3. REGISTRAR - CON CONTROL DE INYECCIÓN DE PRIVILEGIOS HACK
        // ============================================================
        [HttpPost("registrar")]
        [AllowAnonymous]
        public async Task<IActionResult> Registrar([FromBody] Turnify.Api.Models.DTOs.UsuarioRegistroDTO dto)
        {
            _logger.LogInformation("--- 📝 Intento de registro para: {Email} ---", dto?.Email);
            
            if (dto == null) return BadRequest(new { message = "Datos inválidos." });

            // 🧠 BLINDAJE CORPORATIVO: GUIDs de los roles de alto nivel compartidos por el cliente
            var adminRoleGuid = Guid.Parse("6DE2A606-416E-4588-B4EB-CC20856CD80A");
            var superAdminRoleGuid = Guid.Parse("6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43");

            // Si el Payload intenta forzar un Rol Administrativo, validamos las cabeceras secretas
            if (dto.RolId == adminRoleGuid || dto.RolId == superAdminRoleGuid)
            {
                if (!Request.Headers.TryGetValue("X-Admin-Creation-Key", out var tokenCabecera) || 
                    tokenCabecera != "TurnifyAdminSecure2026Key")
                {
                    _logger.LogCritical("🔒 [ALERTA DE SEGURIDAD] Intento malicioso de registrar cuenta administrativa sin clave secreta desde: {Email}", dto.Email);
                    return Unauthorized(new { message = "Acceso denegado. No posees las llaves criptográficas para dar de alta perfiles administrativos." });
                }
                
                _logger.LogInformation("✅ [Seguridad Interna] Registro de administración verificado exitosamente vía Header de red.");
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
        // 4. RECUPERACIÓN DE CONTRASEÑA (Validación Dual - Intacto)
        // ============================================================
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] Turnify.Api.Models.DTOs.ForgotPasswordDto dto)
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
        public async Task<IActionResult> ResetPassword([FromBody] Turnify.Api.Models.DTOs.ResetPasswordDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de petición nulos." });

            var emailInput = dto.Email?.Trim().ToLower() ?? string.Empty;
            var telefonoInput = new string((dto.Telefono ?? "").Where(char.IsDigit).ToArray()); 
            var tokenInput = dto.Token?.Trim() ?? string.Empty;

            _logger.LogInformation("--- 🔑 [Reset-Password Debug] Intento para Email: '{Email}' | Token enviado: '{Token}' | Teléfono filtrado numérico: '{Tel}' ---", emailInput, tokenInput, telefonoInput);

            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => 
                u.ResetToken == tokenInput && !string.IsNullOrEmpty(tokenInput) && u.ResetTokenExpires > DateTime.UtcNow);

            if (usuario != null)
            {
                _logger.LogInformation("Campamento localizado con éxito mediante Token de seguridad.");
            }
            else
            {
                _logger.LogWarning("⚠️ [Reset-Password] Token inválido, vencido o vacío ('{Token}'). Evaluando mecanismo alterno de Validación Dual...", tokenInput);
                
                usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email != null && u.email.ToLower() == emailInput);
                
                if (usuario != null)
                {
                    _logger.LogInformation("🔍 [Reset-Password] Correo base encontrado (User ID: {Id}). Analizando tablas vinculadas...", usuario.id);

                    var esClienteValido = await _context.clientes
                        .AnyAsync(c => c.usuario_id == usuario.id && 
                                       c.email != null && c.email.ToLower() == emailInput &&
                                       c.telefono != null && 
                                       EF.Functions.Like(c.telefono, $"%{telefonoInput}%"));
                    
                    _logger.LogInformation("   -> ¿Cruzó datos en tabla Clientes?: {Status}", esClienteValido);

                    bool matchProveedor = false;
                    try 
                    {
                        matchProveedor = await _context.proveedores
                            .AnyAsync(p => p.UsuarioId == usuario.id && 
                                           p.Email != null && p.Email.ToLower() == emailInput &&
                                           p.Telefono != null && 
                                           EF.Functions.Like(p.Telefono, $"%{telefonoInput}%"));
                        
                        _logger.LogInformation("   -> ¿Cruzó datos en tabla Proveedores?: {Status}", matchProveedor);
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogError("❌ Error executing dual validation in providers: {Msg}", ex.Message); 
                    }

                    if (!esClienteValido && !matchProveedor)
                    {
                        _logger.LogWarning("❌ [Reset-Password] Falló Validación Dual. El teléfono '{Tel}' no tiene correspondencia con el correo '{Email}' en ninguna entidad.", telefonoInput, emailInput);
                        usuario = null; 
                    }
                    else
                    {
                        _logger.LogInformation("✅ [Reset-Password] Validación Dual aprobada con éxito para el usuario '{Email}'.", emailInput);
                    }
                }
            }

            if (usuario == null) 
            {
                return BadRequest(new { message = "Datos de validación incorrectos. Verifique el token o la combinación de correo/teléfono." });
            }

            try 
            {
                usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                usuario.ResetToken = null;
                usuario.ResetTokenExpires = null;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("🎉 [Reset-Password] ¡Contraseña actualizada correctamente en base de datos para {Email}!", emailInput);
                return Ok(new { message = "Contraseña actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "--- 🚨 ERROR CRÍTICO AL GUARDAR PASS ---");
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
            catch (Exception ) { return StatusCode(500, new { message = "Error" }); }
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
        // 6. CRUD BÁSICO (Intacto)
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

        // 🚩 GENERAR TOKEN - AJUSTE OPERACIONAL: Reparación del typo providerId -> proveedorId
        private string GenerarTokenJWT(Usuarios usuario, Guid? clienteId = null, Guid? proveedorId = null)
        {
            var jwtKey = _config["Jwt:Key"] ?? "Clave_Super_Secreta_2026_Turnify_Darwin";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var claims = new List<Claim> { 
                new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()), 
                new Claim(ClaimTypes.Name, usuario.nombre ?? ""), 
                new Claim(ClaimTypes.Role, usuario.Rol?.nombre ?? "Usuario") 
            };

            if (clienteId.HasValue) claims.Add(new Claim("ClienteId", clienteId.Value.ToString()));
            // 🧠 FIX: Cambiado de providerId.HasValue a proveedorId.HasValue para hacer match con el parámetro
            if (proveedorId.HasValue) claims.Add(new Claim("ProveedorId", proveedorId.Value.ToString()));

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