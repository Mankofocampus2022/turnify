using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services
{
    public class ServicioService : IServicioService
    {
        private readonly TurnifyDbContext _context;

        public ServicioService(TurnifyDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ServicioReadDto>> ObtenerTodos()
        {
            var servicios = await _context.servicios
                .Include(s => s.Proveedor)
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<IEnumerable<ServicioReadDto>> ObtenerPorProveedor(Guid proveedorId)
        {
            var servicios = await _context.servicios
                .Where(s => s.ProveedorId == proveedorId) 
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<IEnumerable<ServicioReadDto>> ObtenerActivosPorProveedor(Guid proveedorId)
        {
            var servicios = await _context.servicios
                // 🚩 CORRECCIÓN: Comparación numérica
                .Where(s => s.ProveedorId == proveedorId && s.Activo == 1) 
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<ServicioReadDto?> ObtenerPorId(Guid id)
        {
            var s = await _context.servicios.FindAsync(id);
            if (s == null) return null;
            return MapearADto(s);
        }

        public async Task<ServicioReadDto> CrearServicio(ServicioUpsertDto dto)
        {
            var nuevoServicio = new Servicios
            {
                Id = Guid.NewGuid(),
                ProveedorId = dto.ProveedorId,
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                DuracionMinutos = dto.DuracionMinutos,
                Precio = dto.Precio,
                Categoria = dto.Categoria,
                ComisionPorcentaje = dto.ComisionPorcentaje,
                ImagenUrl = dto.ImagenUrl,
                Activo = 1, // 🚩 CORRECCIÓN: 1 en lugar de true
                FechaCreacion = DateTime.UtcNow
            };

            _context.servicios.Add(nuevoServicio);
            await _context.SaveChangesAsync();
            return MapearADto(nuevoServicio);
        }

        public async Task<ServicioReadDto?> ActualizarServicio(Guid id, ServicioUpsertDto dto)
        {
            var servicio = await _context.servicios.FindAsync(id);
            if (servicio == null) return null;

            servicio.Nombre = dto.Nombre;
            servicio.Descripcion = dto.Descripcion;
            servicio.DuracionMinutos = dto.DuracionMinutos;
            servicio.Precio = dto.Precio;
            servicio.Categoria = dto.Categoria;
            servicio.ComisionPorcentaje = dto.ComisionPorcentaje;
            servicio.ImagenUrl = dto.ImagenUrl;
            servicio.Activo = dto.Activo; // 🚩 Permite actualizar el estado (0, 1, 2)

            await _context.SaveChangesAsync();
            return MapearADto(servicio);
        }

        public async Task<bool> EliminarServicio(Guid id)
        {
            var servicio = await _context.servicios.FindAsync(id);
            if (servicio == null) return false;

            servicio.Activo = 0; // 🚩 CORRECCIÓN: 0 en lugar de false
            await _context.SaveChangesAsync();
            return true;
        }

        private static ServicioReadDto MapearADto(Servicios s) => new ServicioReadDto
        {
            Id = s.Id,
            Nombre = s.Nombre,
            Descripcion = s.Descripcion,
            Precio = s.Precio,
            DuracionMinutos = s.DuracionMinutos,
            Categoria = s.Categoria,
            ImagenUrl = s.ImagenUrl,
            ComisionPorcentaje = s.ComisionPorcentaje,
            Activo = s.Activo // Ambos son INT, aquí no hay lío
        };
    }
}