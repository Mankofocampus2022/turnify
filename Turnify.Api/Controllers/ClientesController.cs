using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;  
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteService _clienteService;
        private readonly TurnifyDbContext _context;

        public ClientesController(IClienteService clienteService, TurnifyDbContext context)
        {
            _clienteService = clienteService;
            _context = context;
        }

        // 🛡️ ENDPOINT MULTI-TENANT PAGINADO BLINDADO CONTRA NULOS (Mitigación OBS-01)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMisClientes([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            var userId = Guid.Parse(usuarioIdClaim);
            
            // 🚀 RESOLUCIÓN DE MULTI-TENANCY MEJORADA: Soporta Proveedores y Rol Staff/Barberos
            Guid proveedorId;

            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            if (proveedor != null)
            {
                proveedorId = proveedor.Id;
            }
            else
            {
                // Si el usuario logueado es un colaborador (Staff), obtenemos el ProveedorId al que pertenece
                var empleado = await _context.empleados
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.UsuarioId == userId);

                proveedorId = empleado != null ? empleado.ProveedorId : userId;
            }

            // 🛡️ FIX CS8602: Filtramos los nulos inmediatamente en la consulta SQL usando Where
            var query = _context.citas
                .AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId)
                .Include(c => c.Cliente)
                .Select(c => c.Cliente)
                .Where(cli => cli != null) // Enseña al compilador que 'cli' jamás será null de aquí en adelante
                .Distinct();

            if (!string.IsNullOrEmpty(search))
            {
                // El operador '!' le confirma a OmniSharp que confiamos en que el campo no vendrá vacío
                query = query.Where(cli => cli!.nombre.Contains(search) || cli!.telefono.Contains(search));
            }

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var clientes = await query
                .OrderBy(cli => cli!.nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(clientes);
        }

        [HttpGet("buscar/{telefono}")]
        public async Task<IActionResult> GetClientePorTelefono(string telefono)
        {
            var cliente = await _clienteService.GetClientePorTelefonoAsync(telefono);
            if (cliente == null) return NotFound("Cliente no registrado.");
            return Ok(cliente);
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> PostCliente(ClienteCreateDto dto)
        {
            var result = await _clienteService.RegistrarClienteAsync(dto);

            if (!result.Success) 
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new 
            { 
                message = result.Message, 
                cliente = result.Cliente 
            });
        }

        [HttpGet("{clienteId}/mis-citas")]
        public async Task<IActionResult> GetMisCitas(Guid clienteId)
        {
            var citas = await _clienteService.GetMisCitasAsync(clienteId);
            return Ok(citas);
        }
    }
}