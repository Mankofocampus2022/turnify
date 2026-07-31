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
                    TipoCobro = e.TipoCobro,
                    ValorBase = e.ValorBase,
                    Estado = e.Estado,
                    // --- CAMPOS DE ACTIVACIÓN TEMPORAL (HU-001-C) ---
                    FechaVencimiento = e.FechaVencimiento,
                    Periodicidad = e.Periodicidad
                })
                .ToListAsync();
        }

        // 🔍 CONSULTAR: Obtener una sola estación por su ID
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
                TipoCobro = estacion.TipoCobro,
                ValorBase = estacion.ValorBase,
                Estado = estacion.Estado,
                // --- CAMPOS DE ACTIVACIÓN TEMPORAL (HU-001-C) ---
                FechaVencimiento = estacion.FechaVencimiento,
                Periodicidad = estacion.Periodicidad
            };
        }

        // ➕ CREAR: Registrar una nueva estación física en el local
        public async Task<EstacionTrabajoResponseDto> CreateAsync(Guid proveedorId, EstacionTrabajoCreateDto dto)
        {
            var nuevaEstacion = new EstacionTrabajo
            {
                Id = Guid.NewGuid(),
                ProveedorId = proveedorId,
                Nombre = string.IsNullOrWhiteSpace(dto.Nombre) ? "Silla Principal" : dto.Nombre.Trim(),
                Activo = true, // Por defecto se registra disponible
                TipoCobro = string.IsNullOrWhiteSpace(dto.TipoCobro) ? "Porcentaje" : dto.TipoCobro,
                ValorBase = dto.ValorBase,
                Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Disponible" : dto.Estado,
                FechaVencimiento = dto.FechaVencimiento,
                Periodicidad = dto.Periodicidad
            };

            _context.estaciones_trabajo.Add(nuevaEstacion);
            await _context.SaveChangesAsync();

            return new EstacionTrabajoResponseDto
            {
                Id = nuevaEstacion.Id,
                ProveedorId = nuevaEstacion.ProveedorId,
                Nombre = nuevaEstacion.Nombre,
                Activo = nuevaEstacion.Activo,
                TipoCobro = nuevaEstacion.TipoCobro,
                ValorBase = nuevaEstacion.ValorBase,
                Estado = nuevaEstacion.Estado,
                FechaVencimiento = nuevaEstacion.FechaVencimiento,
                Periodicidad = nuevaEstacion.Periodicidad
            };
        }

        // 📝 ACTUALIZAR: Modificar el nombre o la disponibilidad de la estación
        public async Task<EstacionTrabajoResponseDto?> UpdateAsync(Guid id, Guid proveedorId, EstacionTrabajoUpdateDto dto)
        {
            var estacion = await _context.estaciones_trabajo
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (estacion == null) return null;

            estacion.Nombre = string.IsNullOrWhiteSpace(dto.Nombre) ? estacion.Nombre : dto.Nombre.Trim();
            estacion.Activo = dto.Activo;

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
                TipoCobro = estacion.TipoCobro,
                ValorBase = estacion.ValorBase,
                Estado = estacion.Estado,
                FechaVencimiento = estacion.FechaVencimiento,
                Periodicidad = estacion.Periodicidad
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

        // =========================================================================
        // 💳 HU-001-B: REGISTRO DE PAGO Y ACTIVACIÓN TEMPORAL DE SILLA / ESTACIÓN
        // =========================================================================
        public async Task<EstacionTrabajoResponseDto?> ActivarEstacionAsync(Guid id, Guid proveedorId, EstacionTrabajoActivarDto dto)
        {
            var estacion = await _context.estaciones_trabajo
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (estacion == null) return null;

            // 1. Determinar Días a Sumar según el Periodo Seleccionado
            int diasASumar = dto.Periodo switch
            {
                "1 Semana" => 7,
                "15 Días" => 15,
                "1 Mes" => 30,
                "1 Trimestre" => 90,
                "1 Semestre" => 180,
                "1 Año" => 365,
                _ => 30
            };

            // 2. Cálculo de Fecha de Vencimiento Acumulativa:
            DateTimeOffset fechaBase = (estacion.FechaVencimiento.HasValue && estacion.FechaVencimiento.Value > DateTimeOffset.UtcNow)
                ? estacion.FechaVencimiento.Value
                : DateTimeOffset.UtcNow;

            estacion.FechaVencimiento = fechaBase.AddDays(diasASumar);
            estacion.Activo = true;
            estacion.Estado = "Disponible";
            estacion.Periodicidad = dto.Periodo;

            await _context.SaveChangesAsync();

            return new EstacionTrabajoResponseDto
            {
                Id = estacion.Id,
                ProveedorId = estacion.ProveedorId,
                Nombre = estacion.Nombre,
                Activo = estacion.Activo,
                TipoCobro = estacion.TipoCobro,
                ValorBase = estacion.ValorBase,
                Estado = estacion.Estado,
                FechaVencimiento = estacion.FechaVencimiento,
                Periodicidad = estacion.Periodicidad
            };
        }
    }
}