using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

        // 🔒 MÉTODO PRIVADO AUXILIAR: Valida firmas mágicas de archivos (MIME Real)
        private static bool EsImagenValida(IFormFile file)
        {
            try
            {
                using (var reader = new BinaryReader(file.OpenReadStream()))
                {
                    var bytes = reader.ReadBytes(8);
                    
                    // JPG/JPEG: FF D8 FF
                    if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;

                    // PNG: 89 50 4E 47 0D 0A 1A 0A
                    if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;

                    // WEBP: RIFF ... WEBP
                    if (bytes.Length >= 8 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return true;

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // 🗑️ MÉTODO PRIVADO AUXILIAR: Elimina la foto física previa si existe en wwwroot
        private static void EliminarFotoAntigua(string? fotoUrl)
        {
            if (string.IsNullOrEmpty(fotoUrl)) return;

            try
            {
                var relativePath = fotoUrl.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Manejo silencioso para no interrumpir el flujo transaccional
            }
        }

        // 🧠 MÉTODO AUXILIAR PRIVADO: Procesa y guarda la imagen del colaborador en disco si viene en el DTO
        private async Task<string?> ProcessFotoUploadAsync(Guid id, IFormFile? fotoFile, string? fotoUrlOriginal)
        {
            // 1. Si enviaron un archivo binario mediante FormData
            if (fotoFile != null && fotoFile.Length > 0)
            {
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(fotoFile.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                {
                    throw new InvalidOperationException("Formato de imagen no permitido. Use .jpg, .jpeg, .png o .webp.");
                }

                if (!EsImagenValida(fotoFile))
                {
                    throw new InvalidOperationException("El archivo adjunto no es una imagen válida o está corrupto.");
                }

                if (fotoFile.Length > 5 * 1024 * 1024)
                {
                    throw new InvalidOperationException("La imagen supera el peso máximo de 5MB.");
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "empleados");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = $"staff_{id}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await fotoFile.CopyToAsync(stream);
                }

                return $"/uploads/empleados/{fileName}";
            }

            // 2. Si no enviaron archivo binario pero enviaron una URL de texto precargada
            if (!string.IsNullOrWhiteSpace(fotoUrlOriginal))
            {
                return fotoUrlOriginal;
            }

            return null;
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
                    FotoUrl = e.FotoUrl, // 🖼️ HU-09: Mapeo de la foto
                    TipoContrato = e.TipoContrato ?? string.Empty,
                    ValorContrato = e.ValorContrato,
                    Activo = e.Activo,
                    EmailUsuarioVinculado = (e.Usuario != null && e.Usuario.email != null) ? e.Usuario.email : "Sin usuario"
                })
                .ToListAsync();
        }

        // 🔍 CONSULTAR: Obtener un solo empleado por su ID
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
                FotoUrl = empleado.FotoUrl, // 🖼️ HU-09: Mapeo de la foto
                TipoContrato = empleado.TipoContrato ?? string.Empty,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = (empleado.Usuario != null && empleado.Usuario.email != null) ? empleado.Usuario.email : "Sin usuario"
            };
        }

        // ➕ CREAR: Registrar empleado (y opcionalmente crearle su cuenta de acceso Staff + Fotografía)
        public async Task<EmpleadoResponseDto> CreateAsync(Guid proveedorId, EmpleadoCreateDto dto)
        {
            Guid? nuevoUsuarioId = null;

            // 🛡️ Lógica de protección: Si se envía correo y clave, se le crea un usuario en el sistema
            if (!string.IsNullOrEmpty(dto.EmailParaUsuario) && !string.IsNullOrEmpty(dto.PasswordParaUsuario))
            {
                var emailLimpio = dto.EmailParaUsuario.Trim().ToLower();
                var existeEmail = await _context.usuarios.AnyAsync(u => u.email == emailLimpio);
                if (existeEmail)
                {
                    throw new InvalidOperationException("El correo electrónico ya está registrado por otro usuario.");
                }

                // 🎯 FIX CRÍTICO: Búsqueda dinámica del ROL DE STAFF para los colaboradores
                var rolStaff = await _context.roles
                    .FirstOrDefaultAsync(r => r.nombre == "Staff" || r.nombre == "Empleado" || r.nombre == Roles.RoleNames.ProveedorDependiente);

                // Asigna explícitamente el GUID del rol Staff: 99a2b3c4-e5f6-4789-90ab-c1d2e3f40099
                var rolIdFinal = rolStaff?.id ?? Guid.Parse("99a2b3c4-e5f6-4789-90ab-c1d2e3f40099");

                var nuevoUsuario = new Usuarios
                {
                    id = Guid.NewGuid(),
                    nombre = dto.Nombre ?? "Colaborador Staff",
                    email = emailLimpio,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordParaUsuario),
                    rol_id = rolIdFinal, // 👈 AHORA SÍ ASIGNA EL ROL DE STAFF
                    fecha_creacion = DateTime.UtcNow,
                    activo = true
                };

                _context.usuarios.Add(nuevoUsuario);
                nuevoUsuarioId = nuevoUsuario.id;

                // 🚩 Registro secundario en Proveedores para soporte multi-tenant (EsIndependiente = false)
                var proveedorPadre = await _context.proveedores.FirstOrDefaultAsync(p => p.Id == proveedorId);
                
                _context.proveedores.Add(new Proveedores
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = nuevoUsuario.id,
                    NombreComercial = dto.Nombre ?? "Colaborador Staff",
                    Tipo = proveedorPadre?.Tipo ?? "Barbería",
                    Email = emailLimpio,
                    Telefono = dto.Telefono ?? string.Empty,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow,
                    Direccion = proveedorPadre?.Direccion ?? "Sede Principal",
                    EsIndependiente = false // 👈 TODO STAFF NACE COMO NO INDEPENDIENTE
                });
            }

            var nuevoEmpleadoId = Guid.NewGuid();

            // 🖼️ HU-08: Procesamiento de la fotografía de perfil
            string? rutaFotoProcesada = await ProcessFotoUploadAsync(nuevoEmpleadoId, dto.Foto, dto.FotoUrl);

            var nuevoEmpleado = new Empleado
            {
                Id = nuevoEmpleadoId,
                ProveedorId = proveedorId,
                UsuarioId = nuevoUsuarioId,
                Nombre = dto.Nombre ?? string.Empty,
                Telefono = dto.Telefono ?? string.Empty,
                FotoUrl = rutaFotoProcesada, // Guardamos la ruta física/URL
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
                FotoUrl = nuevoEmpleado.FotoUrl, // 🖼️ Exponemos la ruta guardada
                TipoContrato = nuevoEmpleado.TipoContrato ?? string.Empty,
                ValorContrato = nuevoEmpleado.ValorContrato,
                Activo = nuevoEmpleado.Activo,
                EmailUsuarioVinculado = dto.EmailParaUsuario ?? "Sin usuario"
            };
        }

        // 📝 ACTUALIZAR: Modificar datos demográficos, condiciones contractuales o fotografía
        public async Task<EmpleadoResponseDto?> UpdateAsync(Guid id, Guid proveedorId, EmpleadoUpdateDto dto)
        {
            var empleado = await _context.empleados
                .Include(e => e.Usuario)
                .FirstOrDefaultAsync(e => e.Id == id && e.ProveedorId == proveedorId);

            if (empleado == null) return null;

            // 🖼️ HU-08: Actualización condicional de la fotografía si se proporciona una nueva
            if (dto.Foto != null || !string.IsNullOrWhiteSpace(dto.FotoUrl))
            {
                var nuevaFotoRuta = await ProcessFotoUploadAsync(id, dto.Foto, dto.FotoUrl);
                if (!string.IsNullOrEmpty(nuevaFotoRuta))
                {
                    // Si ya tenía una foto almacenada localmente y es diferente a la nueva, eliminamos la anterior
                    if (!string.IsNullOrEmpty(empleado.FotoUrl) && empleado.FotoUrl != nuevaFotoRuta)
                    {
                        EliminarFotoAntigua(empleado.FotoUrl);
                    }
                    empleado.FotoUrl = nuevaFotoRuta;
                }
            }

            if (!string.IsNullOrEmpty(dto.Nombre)) empleado.Nombre = dto.Nombre;
            if (!string.IsNullOrEmpty(dto.Telefono)) empleado.Telefono = dto.Telefono;
            if (!string.IsNullOrEmpty(dto.TipoContrato)) empleado.TipoContrato = dto.TipoContrato;
            
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
                FotoUrl = empleado.FotoUrl, // 🖼️ Mapeo de la foto actualizada
                TipoContrato = empleado.TipoContrato ?? string.Empty,
                ValorContrato = empleado.ValorContrato,
                Activo = empleado.Activo,
                EmailUsuarioVinculado = (empleado.Usuario != null && empleado.Usuario.email != null) ? empleado.Usuario.email : "Sin usuario"
            };
        }

        // 🔄 TOGGLE: Desactivación rápida o reactivación sin destruir registros físicos
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
                    FotoUrl = e.FotoUrl, // 🖼️ HU-09: El cliente público también ve la foto del colaborador
                    TipoContrato = e.TipoContrato ?? string.Empty,
                    Activo = e.Activo
                })
                .ToListAsync();
        }
    }
}