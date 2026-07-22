using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services
{
    public class EstacionTrabajoService : IEstacionTrabajoService
    {
        private readonly TurnifyDbContext _context;

        public EstacionTrabajoService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 👥 LISTAR: Obtener todas las estaciones/sillas del negocio
        public async Task<IEnumerable<EstacionTrabajoResponseDto>> GetAllByProveedorAsync(Guid proveedorId)
        {
            return await _context.estaciones_trabajo
                .AsNoTracking()
                .Where(e => e.ProveedorId == proveedorId)
                .Select(e => new EstacionTrabajoResponseDto
                {
                    Id = e.Id,
                    ProveedorId = e.ProveedorId,
                    Nombre = e.Nombre,
                    Activo = e.Activo,
                    // --- NUEVOS CAMPOS ---
                    TipoCobro = e.TipoCobro,
                    ValorBase = e.ValorBase,
                    Estado = e.Estado
                })
                .ToListAsync();
        }

        // 🔍 CONSULTAR: Obtener una sola estación por su ID (Ajuste Nullable CS8613/CS8603)
        public async Task<EstacionTrabajoResponseDto?> GetByIdAsync(Guid id, Guid proveedorId)
        {
            var estacion = await _context.estaciones_trabajo
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (estacion == null) return null;

            return new EstacionTrabajoResponseDto
            {
                Id = estacion.Id,
                ProveedorId = estacion.ProveedorId,
                Nombre = estacion.Nombre,
                Activo = estacion.Activo,
                // --- NUEVOS CAMPOS ---
                TipoCobro = estacion.TipoCobro,
                ValorBase = estacion.ValorBase,
                Estado = estacion.Estado
            };
        }

        // ➕ CREAR: Registrar una nueva estación física en el local
        public async Task<EstacionTrabajoResponseDto> CreateAsync(Guid proveedorId, EstacionTrabajoCreateDto dto)
        {
            var nuevaEstacion = new EstacionTrabajo
            {
                Id = Guid.NewGuid(),
                ProveedorId = proveedorId,
                Nombre = dto.Nombre,
                Activo = true, // Por defecto se registra disponible
                // --- NUEVOS CAMPOS ---
                TipoCobro = string.IsNullOrWhiteSpace(dto.TipoCobro) ? "Porcentaje" : dto.TipoCobro,
                ValorBase = dto.ValorBase,
                Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Disponible" : dto.Estado
            };

            _context.estaciones_trabajo.Add(nuevaEstacion);
            await _context.SaveChangesAsync();

            return new EstacionTrabajoResponseDto
            {
                Id = nuevaEstacion.Id,
                ProveedorId = nuevaEstacion.ProveedorId,
                Nombre = nuevaEstacion.Nombre,
                Activo = nuevaEstacion.Activo,
                // --- NUEVOS CAMPOS ---
                TipoCobro = nuevaEstacion.TipoCobro,
                ValorBase = nuevaEstacion.ValorBase,
                Estado = nuevaEstacion.Estado
            };
        }

        // 📝 ACTUALIZAR: Modificar el nombre o la disponibilidad de la estación (Ajuste Nullable CS8613/CS8603)
        public async Task<EstacionTrabajoResponseDto?> UpdateAsync(Guid id, Guid proveedorId, EstacionTrabajoUpdateDto dto)
        {
            var estacion = await _context.estaciones_trabajo
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (estacion == null) return null;

            estacion.Nombre = dto.Nombre;
            estacion.Activo = dto.Activo;

            // --- NUEVOS CAMPOS ---
            if (!string.IsNullOrWhiteSpace(dto.TipoCobro))
                estacion.TipoCobro = dto.TipoCobro;

            estacion.ValorBase = dto.ValorBase;

            if (!string.IsNullOrWhiteSpace(dto.Estado))
                estacion.Estado = dto.Estado;

            await _context.SaveChangesAsync();

            return new EstacionTrabajoResponseDto
            {
                Id = estacion.Id,
                ProveedorId = estacion.ProveedorId,
                Nombre = estacion.Nombre,
                Activo = estacion.Activo,
                // --- NUEVOS CAMPOS ---
                TipoCobro = estacion.TipoCobro,
                ValorBase = estacion.ValorBase,
                Estado = estacion.Estado
            };
        }

        // 🔄 TOGGLE: Desactivación por mantenimiento o reactivación rápida
        public async Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId)
        {
            var estacion = await _context.estaciones_trabajo
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (estacion == null) return false;

            estacion.Activo = !estacion.Activo;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}