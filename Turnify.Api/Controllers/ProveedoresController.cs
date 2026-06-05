using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; // 🚩 Importante: Usaremos los DTOs que están en tu carpeta DTOs
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization; // 🧠 INYECTADO SENIOR: Namespace necesario para liberar rutas públicas
using Turnify.Api.Interfaces; // 🧠 INYECTADO SENIOR: Requerido para acoplar el motor de paginación de usuarios

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedoresController : ControllerBase
    {
        private readonly TurnifyDbContext _context;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IUsuarioService _usuarioService; // 🧠 INYECTADO SENIOR: Abstracción acoplada para mitigar la OBS-01

        public ProveedoresController(TurnifyDbContext context, IStringLocalizer<Messages> localizer, IUsuarioService usuarioService)
        {
            _context = context;
            _localizer = localizer;
            _usuarioService = usuarioService;
        }

        [HttpGet("test-idioma")]
        public IActionResult TestIdioma()
        {
            var mensaje = _localizer["Welcome"]; 
            return Ok(new { respuesta = mensaje.Value });
        }

        // 🧠 [KILLER FIX QR] - Permitimos acceso anónimo para que clientes sin cuenta carguen la lista de locales
        // 🚀 AJUSTE EFECTIVO OBS-01: Implementación del motor de filtrado y cortes limpios en base de datos
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetProveedores([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            // 🛡️ Inicializamos la consulta base como un IQueryable
            var query = _context.proveedores
                .AsNoTracking() // 🛡️ Evita el desborde de memoria RAM en el servidor productivo bloqueando el rastreo
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado);

            // 🚀 Filtrado dinámico en caliente por nombre comercial o categoría
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.NombreComercial.Contains(search) || p.Categoria.Contains(search));
            }

            // 🛡️ Control defensivo de paginación nula o desbordada
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            // 🚀 Ejecución de desplazamientos (OFFSET / FETCH NEXT) indexados obligatorios para SQL Server
            return await query
                .OrderBy(p => p.NombreComercial)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    // 🧠 INYECTADO SENIOR: Mapeo de salida para listar la categoría en el panel web global
                    p.Categoria,
                    p.TrabajaDomicilio,
                    p.Activo,
                    // 🛡️ FIX NULABILIDAD SUPRESIÓN: Evita alertas falsas de compilación indicando control de nulo explícito
                    Dueno = p.Usuario != null ? p.Usuario.nombre : "Usuario no encontrado"
                })
                .ToListAsync();
        }

        [HttpPut("{id:guid}")] // 🚩 Agregamos :guid para validar la ruta de una vez
        public async Task<IActionResult> UpdatePerfil(Guid id, [FromBody] ProveedorUpdateDto dto)
        {
            // 1. Validación de consistencia
            if (id != dto.Id)
            {
                return BadRequest(new { message = "El ID de la URL no coincide con el del cuerpo." });
            }

            // 2. Buscar el registro
            var proveedor = await _context.proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound(new { message = "Proveedor no encontrado." });
            }

            // 3. Mapeo Manual (Actualizamos solo lo permitido)
            proveedor.NombreComercial = dto.NombreComercial;
            proveedor.Direccion = dto.Direccion;
            proveedor.Tipo = dto.Tipo;
            
            // 🧠 INYECTADO SENIOR: Mapeo de actualización en caliente desde el panel administrativo
            proveedor.Categoria = dto.Categoria ?? proveedor.Categoria;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "¡Perfil actualizado con exito!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al guardar", 
                    details = ex.InnerException?.Message ?? ex.Message 
                });
            }
        }

        // 🧠 [KILLER FIX QR] - Permitimos acceso anónimo para obtener el detalle unitario del negocio escaneado
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetProveedor(Guid id)
        {
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado)
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    // 🧠 INYECTADO SENIOR: Mapeo de salida para la vista unitaria del detalle del perfil
                    p.Categoria,
                    p.UsuarioId,
                    UsuarioNombre = p.Usuario != null ? p.Usuario.nombre : "N/A",
                    p.TrabajaDomicilio,
                    p.Activo
                })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null) return NotFound(new { message = "Proveedor no encontrado" });
            return proveedor;
        }

        [HttpPost]
        public async Task<ActionResult<Proveedores>> PostProveedor([FromBody] ProveedorCreateDto dto)
        {
            var usuarioExiste = await _context.usuarios.AnyAsync(u => u.id == dto.usuarioId);
            if (!usuarioExiste) return BadRequest("El usuario dueño no existe.");

            var nuevoProveedor = new Proveedores
            {
                Id = Guid.NewGuid(),
                NombreComercial = dto.nombre_comercial,
                Direccion = dto.direccion,
                Tipo = dto.tipo,
                // 🧠 INYECTADO SENIOR: Captura el valor exacto enviado por el JSON del frontend, si falta cae en "Barbero"
                Categoria = dto.categoria ?? "Barbero",
                UsuarioId = dto.usuarioId,
                FechaCreacion = DateTime.Now,
                TrabajaDomicilio = dto.trabaja_domicilio,
                Activo = dto.activo,
                Eliminado = false
            };

            _context.proveedores.Add(nuevoProveedor);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProveedor), new { id = nuevoProveedor.Id }, nuevoProveedor);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProveedor(Guid id)
        {
            var proveedor = await _context.proveedores.FindAsync(id);
            if (proveedor == null) return NotFound();

            proveedor.Eliminado = true;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Soft Delete realizado con éxito" });
        }
    }
} 