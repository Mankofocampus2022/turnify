using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IEmpleadoService
    {
        /// <summary>
        /// Trae todos los empleados de un negocio específico (Vista Admin - Incluye inactivos)
        /// </summary>
        Task<IEnumerable<EmpleadoResponseDto>> GetAllByProveedorAsync(Guid proveedorId);

        /// <summary>
        /// Trae un empleado por su ID (Ajustado a anulable ? para sincronizar con la implementación)
        /// </summary>
        Task<EmpleadoResponseDto?> GetByIdAsync(Guid id, Guid proveedorId);

        /// <summary>
        /// 🖼️ HU-08: Crear el empleado (y opcionalmente su usuario Staff de acceso + Fotografía de perfil)
        /// </summary>
        Task<EmpleadoResponseDto> CreateAsync(Guid proveedorId, EmpleadoCreateDto dto);

        /// <summary>
        /// 🖼️ HU-08 & HU-09: Modificar los datos, el contrato o la foto del empleado
        /// </summary>
        Task<EmpleadoResponseDto?> UpdateAsync(Guid id, Guid proveedorId, EmpleadoUpdateDto dto);

        /// <summary>
        /// Borrado lógico o desactivación rápida
        /// </summary>
        Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId);

        /// <summary>
        /// 🚀 HU 001 - PÚBLICO: Traer SOLO los empleados activos para que el cliente elija (Barbero Preferido)
        /// </summary>
        Task<IEnumerable<EmpleadoResponseDto>> GetActivosByProveedorAsync(Guid proveedorId);
    }
}