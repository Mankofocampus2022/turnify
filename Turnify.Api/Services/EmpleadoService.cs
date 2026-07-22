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
    public class EmpleadoService : IEmpleadoService
    {
        private readonly TurnifyDbContext _context;

        public EmpleadoService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 👥 LISTAR: Obtener todos los empleados de un negocio específico
        public async Task<IEnumerable<EmpleadoResponseDto>> GetAllByProveedorAsync(Guid proveedorId)
        {
            return await _context.empleados
                .AsNoTracking()
                .Where(e => e.ProveedorId == proveedorId)
                .Select(e => new EmpleadoResponseDto
                {
                    Id = e.Id,
                    ProveedorId = e.ProveedorId,
                    UsuarioId = e.UsuarioId,
                    Nombre = e.Nombre ?? string.Empty,
                    Telefono = e.Telefono ?? string.Empty,
                    TipoContrato = e.TipoContrato ?? string.Empty,
                    ValorContrato = e.ValorContrato,
                    Activo = e.Activo,
                    EmailUsuarioVinculado = (e.Usuario != null && e.Usuario.email != null) ? e.Usuario.email : "Sin usuario"
                })
                .ToListAsync();
        }

        // 🔍 CONSULTAR: Obtener un solo empleado por su ID (Ajuste Nullable CS8603)
        public async Task<EmpleadoResponseDto?> GetByIdAsync(Guid id, Guid proveedorId)
        {
            var empleado = await _context.empleados
                .AsNoTracking()
                .Include(e => e.Usuario)
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return null;

            return new EmpleadoResponseDto
            {
                Id = empleado.Id,
                ProveedorId = empleado.ProveedorId,
                UsuarioId = empleado.UsuarioId,
                Nombre = empleado.Nombre ?? string.Empty,
                Telefono = empleado.Telefono ?? string.Empty,
                TipoContrato = empleado.TipoContrato ?? string.Empty,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = (empleado.Usuario != null && empleado.Usuario.email != null) ? empleado.Usuario.email : "Sin usuario"
            };
        }

        // ➕ CREAR: Registrar empleado (y opcionalmente crearle su cuenta de acceso Staff)
        public async Task<EmpleadoResponseDto> CreateAsync(Guid proveedorId, EmpleadoCreateDto dto)
        {
            Guid? nuevoUsuarioId = null;

            // 🛡️ Lógica de protección: Si se envía correo y clave, se le crea un usuario en el sistema
            if (!string.IsNullOrEmpty(dto.EmailParaUsuario) && !string.IsNullOrEmpty(dto.PasswordParaUsuario))
            {
                var existeEmail = await _context.usuarios.AnyAsync(u => u.email == dto.EmailParaUsuario);
                if (existeEmail)
                {
                    throw new InvalidOperationException("El correo electrónico ya está registrado por otro usuario.");
                }

                var nuevoUsuario = new Usuarios
                {
                    id = Guid.NewGuid(),
                    email = dto.EmailParaUsuario,
                    // Nota: Se asume el uso de BCrypt.Net para la encriptación de contraseñas de tu login
                    password_hash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordParaUsuario),
                    rol_id = Guid.Parse("99A2B3C4-E5F6-4789-90AB-C1D2E3F40099") // Rol Semilla 'Staff'
                };

                _context.usuarios.Add(nuevoUsuario);
                nuevoUsuarioId = nuevoUsuario.id;
            }

            var nuevoEmpleado = new Empleado
            {
                Id = Guid.NewGuid(),
                ProveedorId = proveedorId,
                UsuarioId = nuevoUsuarioId,
                Nombre = dto.Nombre ?? string.Empty,
                Telefono = dto.Telefono ?? string.Empty,
                TipoContrato = dto.TipoContrato ?? string.Empty,
                ValorContrato = dto.ValorContrato,
                Activo = true
            };

            _context.empleados.Add(nuevoEmpleado);
            
            // Persistencia unificada en base de datos
            await _context.SaveChangesAsync();

            return new EmpleadoResponseDto
            {
                Id = nuevoEmpleado.Id,
                ProveedorId = nuevoEmpleado.ProveedorId,
                UsuarioId = nuevoEmpleado.UsuarioId,
                Nombre = nuevoEmpleado.Nombre ?? string.Empty,
                Telefono = nuevoEmpleado.Telefono ?? string.Empty,
                TipoContrato = nuevoEmpleado.TipoContrato ?? string.Empty,
                ValorContrato = nuevoEmpleado.ValorContrato,
                Activo = nuevoEmpleado.Activo,
                EmailUsuarioVinculado = dto.EmailParaUsuario ?? "Sin usuario"
            };
        }

        // 📝 ACTUALIZAR: Modificar datos demográficos o condiciones contractuales (Ajuste Nullable CS8603)
        public async Task<EmpleadoResponseDto?> UpdateAsync(Guid id, Guid proveedorId, EmpleadoUpdateDto dto)
        {
            var empleado = await _context.empleados
                .Include(e => e.Usuario)
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return null;

            empleado.Nombre = dto.Nombre ?? string.Empty;
            empleado.Telefono = dto.Telefono ?? string.Empty;
            empleado.TipoContrato = dto.TipoContrato ?? string.Empty;
            empleado.ValorContrato = dto.ValorContrato;
            empleado.Activo = dto.Activo;

            await _context.SaveChangesAsync();

            return new EmpleadoResponseDto
            {
                Id = empleado.Id,
                ProveedorId = empleado.ProveedorId,
                UsuarioId = empleado.UsuarioId,
                Nombre = empleado.Nombre ?? string.Empty,
                Telefono = empleado.Telefono ?? string.Empty,
                TipoContrato = empleado.TipoContrato ?? string.Empty,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = (empleado.Usuario != null && empleado.Usuario.email != null) ? empleado.Usuario.email : "Sin usuario"
            };
        }

        // 🔄 TOGGLE: Desactivación rápida o reactivación sin destruir registros físicos (Soft Control)
        public async Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId)
        {
            var empleado = await _context.empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return false;

            empleado.Activo = !empleado.Activo;
            await _context.SaveChangesAsync();
            return true;
        }

        // 🚀 HU 001 - PÚBLICO: Traer SOLO los empleados activos para que el cliente elija (Barbero Preferido)
        public async Task<IEnumerable<EmpleadoResponseDto>> GetActivosByProveedorAsync(Guid proveedorId)
        {
            return await _context.empleados
                .AsNoTracking()
                .Where(e => e.ProveedorId == proveedorId && e.Activo == true)
                .Select(e => new EmpleadoResponseDto
                {
                    Id = e.Id,
                    ProveedorId = e.ProveedorId,
                    Nombre = e.Nombre ?? string.Empty,
                    TipoContrato = e.TipoContrato ?? string.Empty,
                    Activo = e.Activo
                    // No exponemos el teléfono, el salario, ni el email del staff por privacidad hacia el cliente público
                })
                .ToListAsync();
        }
    }
}