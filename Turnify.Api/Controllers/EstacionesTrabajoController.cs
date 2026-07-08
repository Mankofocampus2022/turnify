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
    [Authorize] // 🛡️ Todo el controlador protegido por JWT
    [Route("api/[controller]")]
    [ApiController]
    public class EstacionesTrabajoController : ControllerBase
    {
        private readonly IEstacionTrabajoService _estacionService;
        private readonly TurnifyDbContext _context;

        public EstacionesTrabajoController(IEstacionTrabajoService estacionService, TurnifyDbContext context)
        {
            _estacionService = estacionService;
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

        // 🪑 GET: api/estacionestrabajo
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var estaciones = await _estacionService.GetAllByProveedorAsync(proveedorId.Value);
            return Ok(estaciones);
        }

        // 🔍 GET: api/estacionestrabajo/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var estacion = await _estacionService.GetByIdAsync(id, proveedorId.Value);
            
            if (estacion == null) return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });

            return Ok(estacion);
        }

        // ➕ POST: api/estacionestrabajo
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EstacionTrabajoCreateDto dto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var nuevaEstacion = await _estacionService.CreateAsync(proveedorId.Value, dto);
            
            return CreatedAtAction(nameof(GetById), new { id = nuevaEstacion.Id }, nuevaEstacion);
        }

        // 📝 PUT: api/estacionestrabajo/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] EstacionTrabajoUpdateDto dto)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var estacionActualizada = await _estacionService.UpdateAsync(id, proveedorId.Value, dto);
            
            if (estacionActualizada == null) return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });

            return Ok(estacionActualizada);
        }

        // 🔄 PATCH: api/estacionestrabajo/{id}/toggle
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleEstado(Guid id)
        {
            var proveedorId = await GetProveedorIdAsync();
            if (proveedorId == null) return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });

            var success = await _estacionService.ToggleEstadoAsync(id, proveedorId.Value);
            
            if (!success) return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });

            return Ok(new { message = "Estado de la estación modificado exitosamente." });
        }
    }
}
