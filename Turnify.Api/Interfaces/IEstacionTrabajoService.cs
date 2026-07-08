using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IEstacionTrabajoService
    {
        // Obtener todas las estaciones/sillas del negocio
        Task<IEnumerable<EstacionTrabajoResponseDto>> GetAllByProveedorAsync(Guid proveedorId);

        // Obtener una sola estación por ID
        Task<EstacionTrabajoResponseDto> GetByIdAsync(Guid id, Guid proveedorId);

        // Crear una nueva estación
        Task<EstacionTrabajoResponseDto> CreateAsync(Guid proveedorId, EstacionTrabajoCreateDto dto);

        // Actualizar datos de la estación
        Task<EstacionTrabajoResponseDto> UpdateAsync(Guid id, Guid proveedorId, EstacionTrabajoUpdateDto dto);

        // Activar o desactivar una estación (Soft Control por mantenimiento)
        Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId);
    }
}