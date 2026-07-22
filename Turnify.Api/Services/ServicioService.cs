using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Turnify.Api.Services
{
    public class ServicioService : IServicioService
    {
        private readonly TurnifyDbContext _context;

        public ServicioService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 🛡️ BLINDAJE: Usamos AsNoTracking() para que las consultas de lectura sean más rápidas
        public async Task<IEnumerable<ServicioReadDto>> ObtenerTodos()
        {
            var servicios = await _context.servicios
                .AsNoTracking()
                .Include(s => s.Proveedor)
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<IEnumerable<ServicioReadDto>> ObtenerPorProveedor(Guid proveedorId)
        {
            var servicios = await _context.servicios
                .AsNoTracking()
                .Where(s => s.ProveedorId == proveedorId) 
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<IEnumerable<ServicioReadDto>> ObtenerActivosPorProveedor(Guid proveedorId)
        {
            var servicios = await _context.servicios
                .AsNoTracking()
                // 🚩 CORRECCIÓN: Comparación numérica (1 = Activo)
                .Where(s => s.ProveedorId == proveedorId && s.Activo == 1) 
                .ToListAsync();
            return servicios.Select(s => MapearADto(s));
        }

        public async Task<ServicioReadDto?> ObtenerPorId(Guid id)
        {
            // 🛡️ Blindaje contra búsquedas de IDs vacíos
            if (id == Guid.Empty) return null;

            var s = await _context.servicios
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (s == null) return null;
            return MapearADto(s);
        }

        // 🚩 AJUSTE: Recibe ServicioCreateDto sincronizado con el Controller y JS
        public async Task<ServicioReadDto> CrearServicio(ServicioCreateDto dto)
        {
            var nuevoServicio = new Servicios
            {
                Id = Guid.NewGuid(),
                // 🛡️ MAPEO SINCRONIZADO Y PROTEGIDO DE NULOS (Ajuste CS8601)
                ProveedorId = dto.proveedor_id, 
                Nombre = dto.nombre?.Trim() ?? string.Empty,
                Descripcion = dto.descripcion?.Trim() ?? string.Empty,
                DuracionMinutos = dto.duracion_minutos, // 🚩 FIX CS0117: Sincronizado con tu modelo real
                Precio = dto.precio,
                Categoria = dto.categoria ?? "Barbería",
                ComisionPorcentaje = dto.comision_porcentaje,
                // 🛡️ BLINDAJE DE ESTADO: Convertimos bool (JS) a int (SQL)
                Activo = dto.activo ? 1 : 0, 
                FechaCreacion = DateTime.UtcNow
            };

            _context.servicios.Add(nuevoServicio);
            await _context.SaveChangesAsync();
            
            return MapearADto(nuevoServicio);
        }

        public async Task<ServicioReadDto?> ActualizarServicio(Guid id, ServicioCreateDto dto)
        {
            var servicio = await _context.servicios.FindAsync(id);
            if (servicio == null) return null;

            servicio.Nombre = dto.nombre?.Trim() ?? servicio.Nombre;
            servicio.Descripcion = dto.descripcion?.Trim() ?? servicio.Descripcion;
            servicio.DuracionMinutos = dto.duracion_minutos; // 🚩 Sincronizado
            servicio.Precio = dto.precio;
            servicio.Categoria = dto.categoria ?? servicio.Categoria;
            servicio.ComisionPorcentaje = dto.comision_porcentaje;
            
            // 🛡️ ACTUALIZACIÓN DE ESTADO: Sincronización bool -> int
            servicio.Activo = dto.activo ? 1 : 0;

            // 🛡️ ACTUALIZACIÓN DE PROVEEDOR (Identidad protegida)
            servicio.ProveedorId = dto.proveedor_id;

            await _context.SaveChangesAsync();
            return MapearADto(servicio);
        }

        // 🚩 FIX CS0535: Nombre exacto del contrato de la interfaz
        public async Task<bool> EliminarServicio(Guid id)
        {
            var servicio = await _context.servicios.FindAsync(id);
            if (servicio == null) return false;

            servicio.Activo = 0; // 🚩 Soft delete (0 = Inactivo)
            return await _context.SaveChangesAsync() > 0;
        }

        // 🛡️ MAPEADOR BLINDADO
        private static ServicioReadDto MapearADto(Servicios s)
        {
            if (s == null) return new ServicioReadDto();

            return new ServicioReadDto
            {
                Id = s.Id,
                ProveedorId = s.ProveedorId, // 🚩 LÍNEA KILLER AGREGADA: Ahora el JS podrá filtrar
                Nombre = s.Nombre,
                Descripcion = s.Descripcion,
                Precio = s.Precio,
                DuracionMinutos = s.DuracionMinutos, // 🚩 Mapeo correcto
                Categoria = s.Categoria,
                ImagenUrl = s.ImagenUrl,
                ComisionPorcentaje = s.ComisionPorcentaje,
                Activo = s.Activo 
            };
        }
    }
}