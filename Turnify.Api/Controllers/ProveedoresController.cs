using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Microsoft.Extensions.Localization;

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedoresController : ControllerBase
    {
        private readonly TurnifyDbContext _context;
        private readonly IStringLocalizer<Messages> _localizer;

        // UN SOLO CONSTRUCTOR PARA TODO (Inyección de dependencias)
        public ProveedoresController(TurnifyDbContext context, IStringLocalizer<Messages> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        [HttpGet("test-idioma")]
        public IActionResult TestIdioma()
        {
            // Busca la llave "Welcome" en tus archivos .resx (Resources/Messages.xx.resx)
            var mensaje = _localizer["Welcome"]; 
            return Ok(new { respuesta = mensaje.Value });
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProveedores()
        {
            return await _context.proveedores
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado) 
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    p.TrabajaDomicilio,
                    p.Activo,
                    Dueno = p.Usuario != null ? p.Usuario.nombre : _localizer["UserNotFound"].Value
                })
                .ToListAsync();
        }

                    [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePerfil(Guid id, [FromBody] ProveedorUpdateDto dto)
        {
            // Validamos que el ID de la URL coincida con el del objeto enviado
            if (id != dto.Id)
            {
                return BadRequest(new { message = "Error de consistencia en el ID." });
            }

            // Buscamos el registro real
            var proveedor = await _context.proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound(new { message = "El proveedor no existe en la base de datos." });
            }

            // Mapeo manual (lo más seguro)
            proveedor.NombreComercial = dto.NombreComercial;
            proveedor.Direccion = dto.Direccion;
            proveedor.Tipo = dto.Tipo;

            try
            {
                _context.Entry(proveedor).State = EntityState.Modified;
                await _context.SaveChangesAsync(); // <-- Aquí es donde ocurre la magia
                return Ok(new { message = "¡Perfil actualizado con éxito, mi perro!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno", details = ex.Message });
            }
        }
                    // Método auxiliar necesario para el Try/Catch
                    private bool ProveedorExists(Guid id)
                    {
                        return _context.proveedores.Any(e => e.Id == id);
                    }

            // EL DTO DEBE USAR Guid PARA EL ID
            public class ProveedorUpdateDto
            {
                public Guid Id { get; set; }
                public string NombreComercial { get; set; } = string.Empty;
                public string Direccion { get; set; } = string.Empty;
                public string Tipo { get; set; } = string.Empty;
                // Agrega aquí los campos que necesites actualizar
            }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProveedor(Guid id)
        {
            var proveedor = await _context.proveedores
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado)
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    p.UsuarioId,
                    UsuarioNombre = p.Usuario != null ? p.Usuario.nombre : "N/A",
                    p.TrabajaDomicilio,
                    p.Activo
                })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null) return NotFound(new { message = _localizer["UserNotFound"].Value });
            return proveedor;
        }

        [HttpPost]
        public async Task<ActionResult<Proveedores>> PostProveedor(ProveedorCreateDto dto)
        {
            var usuarioExiste = await _context.usuarios.AnyAsync(u => u.id == dto.usuarioId);
            if (!usuarioExiste) return BadRequest(_localizer["UserNotFound"].Value);

            var nuevoProveedor = new Proveedores
            {
                Id = Guid.NewGuid(),
                NombreComercial = dto.nombre_comercial,
                Direccion = dto.direccion,
                Tipo = dto.tipo,
                UsuarioId = dto.usuarioId,
                FechaCreacion = DateTime.Now,
                TrabajaDomicilio = dto.trabaja_domicilio,
                Activo = dto.activo,
                Eliminado = false
            };

            try
            {
                _context.proveedores.Add(nuevoProveedor);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetProveedor), new { id = nuevoProveedor.Id }, nuevoProveedor);
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Error", 
                    details = ex.InnerException?.Message ?? ex.Message 
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProveedor(Guid id)
        {
            var proveedor = await _context.proveedores.FindAsync(id);
            if (proveedor == null) return NotFound();

            proveedor.Eliminado = true;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Soft Delete OK" });
        }
    }

    public class ProveedorCreateDto
    {
        public string nombre_comercial { get; set; } = string.Empty;
        public string direccion { get; set; } = string.Empty;
        public string tipo { get; set; } = string.Empty;
        public Guid usuarioId { get; set; }
        public bool trabaja_domicilio { get; set; } 
        public bool activo { get; set; } = true;
    }
}