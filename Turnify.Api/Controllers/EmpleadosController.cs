using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Controllers
{
    [Authorize] // 🛡️ Todo el controlador está protegido por token JWT
    [Route("api/[controller]")]
    [ApiController]
    public class EmpleadosController : ControllerBase
    {
        private readonly IEmpleadoService _empleadoService;
        private readonly TurnifyDbContext _context;

        public EmpleadosController(IEmpleadoService empleadoService, TurnifyDbContext context)
        {
            _empleadoService = empleadoService;
            _context = context;
        }

        // 🧠 MÉTODO PRIVADO DE SEGURIDAD: Extrae la identidad del dueño desde el Token
        private async Task<Guid?> GetProveedorIdAsync()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return null;

            if (!Guid.TryParse(userIdString, out var userId)) return null;

            var proveedor = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == userId);
            
            return proveedor?.Id;
        }

        // 🔒 MÉTODO PRIVADO AUXILIAR: Valida firmas mágicas de archivos (MIME Real)
        private static bool EsImagenValida(IFormFile file)
        {
            try
            {
                using (var reader = new BinaryReader(file.OpenReadStream()))
                {
                    var bytes = reader.ReadBytes(8);
                    
                    // JPG/JPEG: FF D8 FF
                    if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;

                    // PNG: 89 50 4E 47 0D 0A 1A 0A
                    if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;

                    // WEBP: RIFF ... WEBP
                    if (bytes.Length >= 8 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return true;

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // 🗑️ MÉTODO PRIVADO AUXILIAR: Elimina la foto física previa si existe
        private void EliminarFotoAntigua(string? fotoUrl)
        {
            if (string.IsNullOrEmpty(fotoUrl)) return;

            try
            {
                var relativePath = fotoUrl.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch
            {
                // Manejo silencioso para no interrumpir el flujo si el archivo no existía físicamente
            }
        }

        // 👥 GET: api/empleados
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var empleados = await _empleadoService.GetAllByProveedorAsync(proveedorId.Value);
            return Ok(empleados);
        }

        // 🔍 GET: api/empleados/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var empleado = await _empleadoService.GetByIdAsync(id, proveedorId.Value);
            
            if (empleado == null) return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

            return Ok(empleado);
        }

        // ➕ POST: api/empleados (Soporta JSON o multipart/form-data para HU-08)
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] EmpleadoCreateDto dto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            try
            {
                var nuevoEmpleado = await _empleadoService.CreateAsync(proveedorId.Value, dto);
                return CreatedAtAction(nameof(GetById), new { id = nuevoEmpleado.Id }, nuevoEmpleado);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 📝 PUT: api/empleados/{id} (Soporta JSON o multipart/form-data para actualización)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] EmpleadoUpdateDto dto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var empleadoActualizado = await _empleadoService.UpdateAsync(id, proveedorId.Value, dto);
            
            if (empleadoActualizado == null) return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

            return Ok(empleadoActualizado);
        }

        // 🗑️ DELETE: api/empleados/{id} (Solución al Error 405 - Eliminación de Empleado)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var empleado = await _context.empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId.Value);

            if (empleado == null) return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

            // Limpieza de foto almacenada en disco si existía
            EliminarFotoAntigua(empleado.FotoUrl);

            _context.empleados.Remove(empleado);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Empleado removido exitosamente." });
        }

        // 🖼️ HU-08: POST: api/empleados/{id}/foto (Endpoint dedicado para la subida directa de fotografía)
        [HttpPost("{id}/foto")]
        public async Task<IActionResult> UploadFoto(Guid id, IFormFile foto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            if (foto == null || foto.Length == 0)
                return BadRequest(new { message = "Debe adjuntar una imagen válida." });

            // CA3: Validar extensiones de imagen permitidas
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();

            if (!extensionesPermitidas.Contains(extension))
                return BadRequest(new { message = "Formato de imagen no soportado. Use .jpg, .jpeg, .png o .webp" });

            // 🛡️ Seguridad Avanzada: Validar cabeceras reales del archivo (Magic Numbers)
            if (!EsImagenValida(foto))
                return BadRequest(new { message = "El archivo adjunto no es una imagen válida o está corrupto." });

            // CA4: Límite de peso (5MB)
            if (foto.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "La imagen supera el peso máximo permitido (5MB)." });

            try
            {
                // Verificar si el empleado existe previamente y obtener su foto actual
                var empleadoExistente = await _empleadoService.GetByIdAsync(id, proveedorId.Value);
                if (empleadoExistente == null)
                    return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

                // Crear directorio de destino en wwwroot/uploads/empleados si no existe
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "empleados");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Nombre único para el archivo
                var fileName = $"staff_{id}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                // Limpieza: Borrar foto anterior del servidor si ya existía una
                EliminarFotoAntigua(empleadoExistente.FotoUrl);

                // Generar URL relativa
                var fotoUrl = $"/uploads/empleados/{fileName}";

                // Actualizar la entidad en BD mediante el servicio
                var dto = new EmpleadoUpdateDto { FotoUrl = fotoUrl };
                var empleadoActualizado = await _empleadoService.UpdateAsync(id, proveedorId.Value, dto);

                return Ok(new { message = "Fotografía actualizada correctamente.", fotoUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error interno al guardar la fotografía: {ex.Message}" });
            }
        }

        // 🔄 PATCH: api/empleados/{id}/toggle
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleEstado(Guid id)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var success = await _empleadoService.ToggleEstadoAsync(id, proveedorId.Value);
            
            if (!success) return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

            return Ok(new { message = "Estado del empleado modificado exitosamente." });
        }

        // 🚀 HU 001 - PÚBLICO: GET: api/empleados/activos/{proveedorId}
        [HttpGet("activos/{proveedorId}")]
        [AllowAnonymous] // Permite el acceso sin token para clientes que escanean el QR
        public async Task<IActionResult> GetActivosPúblico(Guid proveedorId)
        {
            if (proveedorId == Guid.Empty) return BadRequest(new { message = "ID de negocio no válido." });

            var empleadosActivos = await _empleadoService.GetActivosByProveedorAsync(proveedorId);
            
            return Ok(empleadosActivos);
        }
    }
}