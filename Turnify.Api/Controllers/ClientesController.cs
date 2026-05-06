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
        private readonly TurnifyDbContext _context; // 🚩 NUEVO: Inyectamos el DB Context para el aislamiento de datos

        public ClientesController(IClienteService clienteService, TurnifyDbContext context)
        {
            _clienteService = clienteService;
            _context = context;
        }

        // 🛡️ NUEVO ENDPOINT SENIOR: Trae solo los clientes atendidos por ESTE proveedor
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMisClientes()
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim)) 
                return Unauthorized(new { message = "Sesión no válida o expirada" });

            var userId = Guid.Parse(usuarioIdClaim);
            
            // A. Buscamos el perfil de proveedor asociado al usuario logueado
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

            var proveedorId = proveedor != null ? proveedor.Id : userId;

            // B. 🛡️ FILTRO MULTI-TENANT: Traemos solo los clientes que tienen citas con este proveedor
            var clientes = await _context.citas
                .AsNoTracking()
                .Where(c => c.ProveedorId == proveedorId)
                .Include(c => c.Cliente)
                .Select(c => c.Cliente)
                .Where(cli => cli != null)
                .Distinct() // Evitamos duplicados si el cliente ha ido varias veces
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
            // 1. Llamamos al servicio usando el DTO
            var result = await _clienteService.RegistrarClienteAsync(dto);

            // 2. Si algo sale mal (ej. teléfono duplicado), avisamos
            if (!result.Success) 
            {
                return BadRequest(new { message = result.Message });
            }

            // 3. Si todo sale bien, devolvemos el objeto creado
            return Ok(new 
            { 
                message = result.Message, 
                cliente = result.Cliente 
            });
        }

        // Ahora el controlador solo llama al servicio y no toca las propiedades directamente
        [HttpGet("{clienteId}/mis-citas")]
        public async Task<IActionResult> GetMisCitas(Guid clienteId)
        {
            var citas = await _clienteService.GetMisCitasAsync(clienteId);
            return Ok(citas);
        }
    }
}