using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services
{
    public class ClienteService : IClienteService
    {
        private readonly TurnifyDbContext _context;

        public ClienteService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 1. EL QUE TE FALTABA (AQUÍ VA)
        public async Task<IEnumerable<Clientes>> GetClientesByUsuarioAsync(Guid usuarioId)
        {
            return await _context.clientes
                .Where(c => c.usuario_id == usuarioId)
                .ToListAsync();
        }

        // 2. Buscar por teléfono
        public async Task<Clientes?> GetClientePorTelefonoAsync(string telefono)
        {
            return await _context.clientes.FirstOrDefaultAsync(c => c.telefono == telefono);
        }

        // 3. Registrar nuevo cliente (Ajustado para incluir usuario_id)
        public async Task<(bool Success, string Message, Clientes? Cliente)> RegistrarClienteAsync(ClienteCreateDto dto)
        {
            var existe = await _context.clientes.AnyAsync(c => c.telefono == dto.Telefono);
            if (existe) return (false, "Ya existe un cliente con ese número de teléfono.", null);

            var nuevoCliente = new Clientes
            {
                id = Guid.NewGuid(),
                nombre = dto.Nombre,
                telefono = dto.Telefono,
                email = dto.Email,
                usuario_id = dto.UsuarioId, // <--- ¡Vital para saber de quién es el cliente!
                fecha_creacion = DateTime.Now
            };

            _context.clientes.Add(nuevoCliente);
            await _context.SaveChangesAsync();

            return (true, "Cliente registrado con éxito", nuevoCliente);
        }

        // 4. Obtener lista con buscador
        public async Task<IEnumerable<Clientes>> GetClientesAsync(string? search)
        {
            var q = _context.clientes.AsQueryable();
            if (!string.IsNullOrEmpty(search)) 
                q = q.Where(c => c.nombre.Contains(search) || c.telefono.Contains(search));
            
            return await q.ToListAsync();
        }

        // 5. Obtener citas
        public async Task<IEnumerable<object>> GetMisCitasAsync(Guid clienteId)
        {
            return await _context.citas
                .Include(c => c.Servicio)
                .Include(c => c.Proveedor)
                .Where(c => c.ClienteId == clienteId)
                .OrderByDescending(c => c.Fecha)
                .Select(c => new {
                    c.Id,
                    c.Fecha,
                    c.Hora,
                    Servicio = c.Servicio != null ? c.Servicio.Nombre : "Servicio no definido",
                    Barberia = c.Proveedor != null ? c.Proveedor.NombreComercial : "Establecimiento no definido",
                    c.Estado,
                    c.PrecioPactado
                })
                .ToListAsync();
        }
    }
}