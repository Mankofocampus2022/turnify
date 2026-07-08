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
                    Nombre = e.Nombre,
                    Telefono = e.Telefono,
                    TipoContrato = e.TipoContrato,
                    ValorContrato = e.ValorContrato,
                    Activo = e.Activo,
                    EmailUsuarioVinculado = e.Usuario != null ? e.Usuario.email : "Sin usuario"
                })
                .ToListAsync();
        }

        // 🔍 CONSULTAR: Obtener un solo empleado por su ID
        public async Task<EmpleadoResponseDto> GetByIdAsync(Guid id, Guid proveedorId)
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
                Nombre = empleado.Nombre,
                Telefono = empleado.Telefono,
                TipoContrato = empleado.TipoContrato,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = empleado.Usuario != null ? empleado.Usuario.email : "Sin usuario"
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
                Nombre = dto.Nombre,
                Telefono = dto.Telefono,
                TipoContrato = dto.TipoContrato,
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
                Nombre = nuevoEmpleado.Nombre,
                Telefono = nuevoEmpleado.Telefono,
                TipoContrato = nuevoEmpleado.TipoContrato,
                ValorContrato = nuevoEmpleado.ValorContrato,
                Activo = nuevoEmpleado.Activo,
                EmailUsuarioVinculado = dto.EmailParaUsuario ?? "Sin usuario"
            };
        }

        // 📝 ACTUALIZAR: Modificar datos demográficos o condiciones contractuales
        public async Task<EmpleadoResponseDto> UpdateAsync(Guid id, Guid proveedorId, EmpleadoUpdateDto dto)
        {
            var empleado = await _context.empleados
                .Include(e => e.Usuario)
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return null;

            empleado.Nombre = dto.Nombre;
            empleado.Telefono = dto.Telefono;
            empleado.TipoContrato = dto.TipoContrato;
            empleado.ValorContrato = dto.ValorContrato;
            empleado.Activo = dto.Activo;

            await _context.SaveChangesAsync();

            return new EmpleadoResponseDto
            {
                Id = empleado.Id,
                ProveedorId = empleado.ProveedorId,
                UsuarioId = empleado.UsuarioId,
                Nombre = empleado.Nombre,
                Telefono = empleado.Telefono,
                TipoContrato = empleado.TipoContrato,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = empleado.Usuario != null ? empleado.Usuario.email : "Sin usuario"
            };
        }

        // 🔄 TOGGLE: Desactivación rápida o reactivación sin destruir registros físcos (Soft Control)
        public async Task<bool> ToggleEstadoAsync(Guid id, Guid proveedorId)
        {
            var empleado = await _context.empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return false;

            empleado.Activo = !empleado.Activo;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}