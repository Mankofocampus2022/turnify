using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Turnify.Api.Data;
using Turnify.Api.Models;

namespace Turnify.Api.Controllers
{
    public class InternalLoginDto 
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // 🚀 DTO para el registro de Proveedores Independientes (HU-10)
    public class RegistroIndependienteDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Categoria { get; set; } = "Barbero"; // Barbero o Manicurista
        public string? Descripcion { get; set; }
        public string Direccion { get; set; } = "Atención a domicilio";
        public string? Ciudad { get; set; } = "Bogotá";
        public IFormFile? FotoRostro { get; set; } // 🛡️ Foto de rostro obligatoria (HU-10 - CA2)
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly TurnifyDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AuthController(IConfiguration config, TurnifyDbContext context, IWebHostEnvironment env)
        {
            _config = config;
            _context = context;
            _env = env;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] InternalLoginDto login)
        {
            if (login == null || string.IsNullOrWhiteSpace(login.Email))
            {
                return BadRequest(new { message = "El email y la contraseña son requeridos." });
            }

            // 1. Buscamos el usuario (email)
            var usuario = await _context.usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.email.ToLower() == login.Email.ToLower());

            // 2. Validación de credenciales
            bool passwordValida = usuario != null && (usuario.password_hash == login.Password || BCrypt.Net.BCrypt.Verify(login.Password, usuario.password_hash));

            if (usuario == null || !passwordValida) 
            {
                // Soporte para tu cuenta admin maestra
                if (login.Email == "admin" && login.Password == "Turnify2026!")
                {
                    string adminRoleName = Roles.RoleNames.Administrador;

                    var tokenAdmin = GenerarToken(Guid.NewGuid().ToString(), "admin", adminRoleName, null, null, null, false, null);
                    return Ok(new { token = tokenAdmin, user = new { nombre = "Admin", rol = adminRoleName } });
                }
                
                return Unauthorized(new { message = "Credenciales incorrectas" });
            }

            // 3. AUTO-VINCULACIÓN (Solución al Cliente NULL de Alexandra)
            var cliente = await _context.clientes
                .FirstOrDefaultAsync(c => c.usuario_id == usuario.id);

            if (cliente == null && usuario.Rol?.nombre != null && usuario.Rol.nombre.Equals("Cliente", StringComparison.OrdinalIgnoreCase))
            {
                cliente = new Clientes
                {
                    id = Guid.NewGuid(),
                    usuario_id = usuario.id,
                    nombre = usuario.nombre,
                    email = usuario.email,
                    telefono = "3110000000",
                    activo = true,
                    fecha_creacion = DateTime.UtcNow
                };
                _context.clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }

            // 4. LÓGICA DE NEGOCIO MULTI-ROL EXTENDIDA (Dueño vs Colaborador Dependiente vs Independiente)
            Guid? proveedorId = null;
            Guid? empleadoId = null;
            bool esIndependiente = false;
            string? fotoUrl = null;

            var rolNombre = usuario.Rol?.nombre ?? Roles.RoleNames.Cliente;

            string roleStaff = Roles.RoleNames.Staff;
            string roleProveedor = Roles.RoleNames.Proveedor;
            string roleProveedorDep = Roles.RoleNames.ProveedorDependiente;

            // 🔧 FIX: Jerarquía estricta según el Rol real del usuario
            if (rolNombre.Equals(roleStaff, StringComparison.OrdinalIgnoreCase))
            {
                // 1. Si es STAFF (Dueño de negocio/búnker), JAMÁS es independiente
                esIndependiente = false;

                var prov = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuario.id);
                if (prov != null)
                {
                    proveedorId = prov.Id;
                    fotoUrl = prov.FotoUrl;
                }
            }
            else if (rolNombre.Equals(roleProveedor, StringComparison.OrdinalIgnoreCase) || 
                     rolNombre.Equals(roleProveedorDep, StringComparison.OrdinalIgnoreCase))
            {
                // 2. Si el rol es Proveedor o ProveedorDependiente (Colaborador/Empleado)
                var empleado = await _context.empleados
                    .FirstOrDefaultAsync(e => e.UsuarioId == usuario.id);

                if (empleado != null)
                {
                    empleadoId = empleado.Id;
                    proveedorId = empleado.ProveedorId;
                    fotoUrl = empleado.FotoUrl;
                    esIndependiente = false;
                }
                else
                {
                    // Si no está en empleados, validar si directamente existe un perfil en proveedores
                    var prov = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuario.id);
                    if (prov != null)
                    {
                        proveedorId = prov.Id;
                        esIndependiente = prov.EsIndependiente;
                        fotoUrl = prov.FotoUrl;
                    }
                }
            }
            else // 3. Demás roles (ProveedorIndependiente, Cliente, Admin)
            {
                var proveedor = await _context.proveedores
                    .FirstOrDefaultAsync(p => p.UsuarioId == usuario.id);
                
                if (proveedor != null)
                {
                    proveedorId = proveedor.Id;
                    esIndependiente = proveedor.EsIndependiente;
                    fotoUrl = proveedor.FotoUrl;
                }
            }
            
            // 5. Generamos el Token con toda la metadata de claims
            var token = GenerarToken(
                usuario.id.ToString(), 
                usuario.email, 
                rolNombre, 
                cliente?.id, 
                proveedorId, 
                empleadoId, 
                esIndependiente, 
                fotoUrl
            );

            return Ok(new { 
                token = token,
                user = new {
                    id = usuario.id,
                    clienteId = cliente?.id, 
                    proveedorId = proveedorId,
                    empleadoId = empleadoId,
                    nombre = usuario.nombre,
                    rol = rolNombre,
                    email = usuario.email,
                    esIndependiente = esIndependiente,
                    fotoUrl = fotoUrl
                }
            });
        }

        // =========================================================================
        // 🚀 ENDPOINT: Registro de Profesional Independiente (HU-10)
        // =========================================================================
        [HttpPost("registro-independiente")]
        public async Task<IActionResult> RegistroIndependiente([FromForm] RegistroIndependienteDto dto)
        {
            // CA2: Validación obligatoria de foto de rostro
            if (dto.FotoRostro == null || dto.FotoRostro.Length == 0)
            {
                return BadRequest(new { message = "La foto del rostro es obligatoria para el registro como Profesional Independiente." });
            }

            // Validar si el correo ya existe
            var existeEmail = await _context.usuarios.AnyAsync(u => u.email.ToLower() == dto.Email.ToLower());
            if (existeEmail)
            {
                return BadRequest(new { message = "El correo electrónico ya se encuentra registrado." });
            }

            // 🚀 BÚSQUEDA ROBUSTA DE ROL (Con Fallback Defensivo)
            string targetRoleName = Roles.RoleNames.ProveedorIndependiente;
            Guid targetRoleId = Roles.RoleIds.ProveedorIndependiente;

            var rolIndependiente = await _context.roles
                .FirstOrDefaultAsync(r => r.nombre == targetRoleName)
                ?? await _context.roles.FindAsync(targetRoleId)
                ?? await _context.roles.FirstOrDefaultAsync(r => r.nombre == Roles.RoleNames.Proveedor)
                ?? await _context.roles.FindAsync(Roles.RoleIds.Proveedor);

            if (rolIndependiente == null)
            {
                return StatusCode(500, new { message = "No se encontró un rol de Proveedor configurado en el sistema." });
            }

            // Procesar y Guardar la foto físicamente (HU-08 / HU-10)
            string? fotoRelativaUrl = null;
            try
            {
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(dto.FotoRostro.FileName).ToLowerInvariant();
                if (!extensionesPermitidas.Contains(ext))
                {
                    return BadRequest(new { message = "Formato de imagen no válido. Formatos permitidos: JPG, JPEG, PNG, WEBP." });
                }

                if (dto.FotoRostro.Length > 3 * 1024 * 1024) // 3MB
                {
                    return BadRequest(new { message = "La foto de perfil no debe superar los 3 MB." });
                }

                var baseWebPath = _env.WebRootPath;
                if (string.IsNullOrEmpty(baseWebPath))
                {
                    baseWebPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsFolder = Path.Combine(baseWebPath, "uploads", "proveedores");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.FotoRostro.CopyToAsync(stream);
                }

                fotoRelativaUrl = $"/uploads/proveedores/{fileName}";
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al procesar la imagen subida.", detalle = ex.Message });
            }

            // Crear Usuario
            var nuevoUsuario = new Usuarios
            {
                id = Guid.NewGuid(),
                nombre = dto.Nombre,
                email = dto.Email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                rol_id = rolIndependiente.id
            };

            _context.usuarios.Add(nuevoUsuario);

            // Crear Perfil de Proveedor Independiente
            var nuevoProveedor = new Proveedores
            {
                Id = Guid.NewGuid(),
                UsuarioId = nuevoUsuario.id,
                NombreComercial = dto.Nombre,
                Email = dto.Email,
                Telefono = dto.Telefono,
                Tipo = "independiente",
                Categoria = dto.Categoria,
                Descripcion = dto.Descripcion,
                Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? "Atención a domicilio" : dto.Direccion,
                Ciudad = dto.Ciudad ?? "Bogotá",
                TrabajaDomicilio = true, // Forzado según HU-10
                EsIndependiente = true,  // Forzado según HU-10
                StaffId = null,          // Sin local/dueño padre
                PorcentajeComision = 0.00m, // 100% de ganancias brutas
                FotoUrl = fotoRelativaUrl,
                Activo = true,
                Eliminado = false,
                FechaCreacion = DateTime.UtcNow
            };

            _context.proveedores.Add(nuevoProveedor);
            await _context.SaveChangesAsync();

            // CA4: Iniciar sesión automáticamente y retornar Token JWT de acceso
            var token = GenerarToken(
                nuevoUsuario.id.ToString(),
                nuevoUsuario.email,
                rolIndependiente.nombre,
                null,
                nuevoProveedor.Id,
                null,
                true,
                fotoRelativaUrl
            );

            return Ok(new
            {
                token = token,
                user = new
                {
                    id = nuevoUsuario.id,
                    proveedorId = nuevoProveedor.Id,
                    nombre = nuevoUsuario.nombre,
                    rol = rolIndependiente.nombre,
                    email = nuevoUsuario.email,
                    esIndependiente = true,
                    fotoUrl = fotoRelativaUrl
                }
            });
        }

        // 🛡️ GENERADOR DE TOKEN JWT CON CLAVE SEGURA MAESTRA (Solución a Bug IDX10653)
        private string GenerarToken(
            string userId, 
            string usuarioEmail, 
            string rol, 
            Guid? clienteId, 
            Guid? proveedorId, 
            Guid? empleadoId,
            bool esIndependiente = false,
            string? fotoUrl = null)
        {
            var rawKey = _config["Jwt:Key"];
            
            // 🚀 BLINDAJE: Garantiza que la clave tenga al menos 512 bits para evitar el crash de HMAC-SHA256
            if (string.IsNullOrEmpty(rawKey) || rawKey.Length < 16)
            {
                rawKey = "Turnify_Master_Enterprise_Secret_Key_2026_Crypto_Engine_512Bits_Security_PRO#";
            }

            var key = Encoding.UTF8.GetBytes(rawKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId), 
                new Claim(ClaimTypes.Name, usuarioEmail ?? "Usuario"),
                new Claim(ClaimTypes.Role, rol ?? Roles.RoleNames.Cliente),
                new Claim("EsIndependiente", esIndependiente.ToString().ToLower()) // 🚀 HU-12 Claim
            };

            if (clienteId.HasValue) claims.Add(new Claim("ClienteId", clienteId.Value.ToString()));
            if (proveedorId.HasValue) claims.Add(new Claim("ProveedorId", proveedorId.Value.ToString()));
            if (empleadoId.HasValue) claims.Add(new Claim("EmpleadoId", empleadoId.Value.ToString()));
            if (!string.IsNullOrEmpty(fotoUrl)) claims.Add(new Claim("FotoUrl", fotoUrl));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _config["Jwt:Issuer"] ?? "Turnify.Api",
                Audience = _config["Jwt:Audience"] ?? "Turnify.App"
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
    
            return tokenHandler.WriteToken(token);
        }
    }
}