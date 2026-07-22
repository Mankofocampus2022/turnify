using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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

            var userId = Guid.Parse(userIdString);
            var proveedor = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == userId);
            
            return proveedor?.Id;
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

        // ➕ POST: api/empleados
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EmpleadoCreateDto dto)
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
                // Atrapa el error si el correo electrónico ya existe en la base de datos
                return BadRequest(new { message = ex.Message });
            }
        }

        // 📝 PUT: api/empleados/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] EmpleadoUpdateDto dto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var empleadoActualizado = await _empleadoService.UpdateAsync(id, proveedorId.Value, dto);
            
            if (empleadoActualizado == null) return NotFound(new { message = "Empleado no encontrado o no pertenece a tu negocio." });

            return Ok(empleadoActualizado);
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
            
            // Siempre devolvemos 200 OK, incluso si la lista está vacía, para no romper el frontend
            return Ok(empleadosActivos);
        }
    }
}