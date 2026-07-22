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

        public async Task<IEnumerable<Clientes>> GetClientesByUsuarioAsync(Guid usuarioId)
        {
            return await _context.clientes
                .Where(c => c.usuario_id == usuarioId)
                .ToListAsync();
        }

        public async Task<Clientes?> GetClientePorTelefonoAsync(string telefono)
        {
            return await _context.clientes.FirstOrDefaultAsync(c => c.telefono == telefono);
        }

        public async Task<(bool Success, string Message, Clientes? Cliente)> RegistrarClienteAsync(ClienteCreateDto dto)
        {
            var existe = await _context.clientes.AnyAsync(c => c.telefono == dto.Telefono);
            if (existe) return (false, "Ya existe un cliente con ese número de teléfono.", null);

            var nuevoCliente = new Clientes
            {
                id = Guid.NewGuid(),
                // 🛡️ FIX CS8601: Coalescencia nula para garantizar que no se asigne null
                nombre = dto.Nombre ?? string.Empty,
                telefono = dto.Telefono ?? string.Empty,
                email = dto.Email,
                usuario_id = dto.UsuarioId,
                fecha_creacion = DateTime.Now
            };

            _context.clientes.Add(nuevoCliente);
            await _context.SaveChangesAsync();

            return (true, "Cliente registrado con éxito", nuevoCliente);
        }

        public async Task<IEnumerable<Clientes>> GetClientesAsync(string? search)
        {
            var q = _context.clientes.AsQueryable();
            if (!string.IsNullOrEmpty(search)) 
            {
                // Protegemos la búsqueda contra propiedades que puedan ser nulas (Ajuste CS8601)
                q = q.Where(c => (c.nombre != null && c.nombre.Contains(search)) || 
                                 (c.telefono != null && c.telefono.Contains(search)));
            }
            
            return await q.ToListAsync();
        }

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

        public async Task<IEnumerable<Clientes>> GetClientesPaginadosAsync(int page, int pageSize, string? search)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _context.clientes
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => (c.nombre != null && c.nombre.Contains(search)) || 
                                         (c.telefono != null && c.telefono.Contains(search)));
            }

            return await query
                .OrderBy(c => c.nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}