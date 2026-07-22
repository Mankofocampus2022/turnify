using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IEmpleadoService
    {
        // Traer todos los empleados de un negocio específico (Vista Admin - Incluye inactivos)
        Task<IEnumerable<EmpleadoResponseDto>> GetAllByProveedorAsync(Guid proveedorId);

        // Traer un empleado por su ID
        Task<EmpleadoResponseDto> GetByIdAsync(Guid id, Guid proveedorId);

        // Crear el empleado (y opcionalmente su usuario Staff de acceso)
        Task<EmpleadoResponseDto> CreateAsync(Guid proveedorId, EmpleadoCreateDto dto);

        // Modificar los datos o el contrato del empleado
        Task<EmpleadoResponseDto> UpdateAsync(Guid id, Guid proveedorId, EmpleadoUpdateDto dto);

        // Borrado lógico o desactivación rápida
        Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId);

        // 🚀 HU 001 - PÚBLICO: Traer SOLO los empleados activos para que el cliente elija (Barbero Preferido)
        Task<IEnumerable<EmpleadoResponseDto>> GetActivosByProveedorAsync(Guid proveedorId);
    }
}