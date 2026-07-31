using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Turnify.Api.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly TurnifyDbContext _context;

        public UsuarioService(TurnifyDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message, Guid? UsuarioId)> RegistrarAsync(UsuarioRegistroDTO dto)
        {
            // 🛡️ Estrategia de ejecución para manejar reintentos en SQL
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<(bool Success, string Message, Guid? UsuarioId)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // 🛡️ Blindaje 1: Normalización de entrada
                    var emailNormalizado = dto.Email?.Trim().ToLower() ?? string.Empty;
                    var telefonoLimpio = new string((dto.Telefono ?? "").Where(char.IsDigit).ToArray());

                    var existeEmail = await _context.usuarios.AnyAsync(u => u.email == emailNormalizado);
                    if (existeEmail) 
                    {
                        return (false, "El correo electrónico ya se encuentra registrado.", (Guid?)null);
                    }

                    // 1. CREACIÓN DEL USUARIO BASE
                    var usuario = new Usuarios {
                        id = Guid.NewGuid(),
                        nombre = dto.Nombre?.Trim() ?? string.Empty,
                        email = emailNormalizado,
                        password_hash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                        rol_id = dto.RolId,
                        fecha_creacion = DateTime.UtcNow,
                        activo = true,
                        suscripcion_fin = DateTime.UtcNow.AddDays(30),
                        esta_bloqueado = false
                    };
                    _context.usuarios.Add(usuario);

                    // 2. IDENTIFICACIÓN DE ROLES OFICIALES SEGÚN BASE DE DATOS
                    var idCliente = Roles.RoleIds.Cliente;               // 56992f75-6420-4d55-a5f9-9223248c50d7
                    var idProveedor = Roles.RoleIds.Proveedor;           // 8854c07c-6e5e-4876-a29a-c7ad5dcfbab7
                    var idStaff = Roles.RoleIds.Staff;                   // 99A2B3C4-E5F6-4789-90AB-C1D2E3F40099
                    var idSuperAdmin = Roles.RoleIds.SuperAdministrador; // 6DE2A606-416E-4588-B4EB-CC20856CD80A
                    var idAdmin = Roles.RoleIds.Administrador;          // 6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43

                    // 3. ESPECIALIZACIÓN DE PERFIL (Solución al Bug del Rol Staff)
                    if (dto.RolId == idCliente) 
                    {
                        _context.clientes.Add(new Clientes {
                            id = Guid.NewGuid(),
                            usuario_id = usuario.id,
                            nombre = string.IsNullOrEmpty(usuario.nombre) ? "Cliente" : usuario.nombre,
                            telefono = telefonoLimpio,
                            email = emailNormalizado,      
                            activo = true,
                            fecha_creacion = DateTime.UtcNow
                        });
                    }
                    // 🚀 FIX: Si se registra como STAFF (Dueño de Búnker / Administrador de local)
                    else if (dto.RolId == idStaff)
                    {
                        _context.proveedores.Add(new Proveedores {
                            Id = Guid.NewGuid(),
                            UsuarioId = usuario.id,
                            NombreComercial = dto.NombreComercial ?? $"Búnker de {(string.IsNullOrEmpty(usuario.nombre) ? "Staff" : usuario.nombre)}",
                            Tipo = dto.TipoNegocio ?? "Barbería",
                            Categoria = "Local Comercial / Búnker",
                            Email = emailNormalizado, 
                            Telefono = telefonoLimpio,
                            Activo = true,
                            FechaCreacion = DateTime.UtcNow,
                            Direccion = "Pendiente de configuración",
                            EsIndependiente = false, // Staff/Dueño Administra Local
                            StaffId = null,          // Es la cabeza del local
                            PorcentajeComision = 0.00m
                        });
                    }
                    // Si se registra como PROVEEDOR (Barbero o Colaborador)
                    else if (dto.RolId == idProveedor) 
                    {
                        _context.proveedores.Add(new Proveedores {
                            Id = Guid.NewGuid(),
                            UsuarioId = usuario.id,
                            NombreComercial = dto.NombreComercial ?? $"Barbería de {(string.IsNullOrEmpty(usuario.nombre) ? "Usuario" : usuario.nombre)}",
                            Tipo = dto.TipoNegocio ?? "Barbería",
                            Email = emailNormalizado, 
                            Telefono = telefonoLimpio,
                            Activo = true,
                            FechaCreacion = DateTime.UtcNow,
                            Direccion = "Pendiente de configuración",
                            EsIndependiente = dto.EsIndependiente
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    return (true, "Registro procesado exitosamente.", (Guid?)usuario.id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"--- 🚨 ERROR CRÍTICO EN REGISTRO: {ex.Message} ---");
                    throw; 
                }
            });
        }

        public async Task<(bool Success, string Message, object? Data)> LoginAsync(LoginDto dto) {
            var emailInput = dto.Email?.Trim().ToLower();
            
            var u = await _context.usuarios
                .Include(x => x.Rol)
                .FirstOrDefaultAsync(x => x.email == emailInput);
            
            if (u == null || !BCrypt.Net.BCrypt.Verify(dto.Password, u.password_hash)) 
                return (false, "Credenciales incorrectas.", null);
                
            return (true, "OK", u);
        }

        public async Task<int> GetTotalUsuariosActivosAsync() {
            return await _context.usuarios.CountAsync(u => u.activo == true);
        }

        // --- MÉTODOS CRUD (Estructura intacta) ---
        public async Task<Usuarios?> GetUsuarioByIdAsync(Guid id) => await _context.usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.id == id);
        public async Task<bool> ActualizarAsync(Usuarios u) { _context.Entry(u).State = EntityState.Modified; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> EliminarLogicoAsync(Guid id) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.activo = false; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool b) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.esta_bloqueado = b; return await _context.SaveChangesAsync() > 0; }
        public async Task<IEnumerable<Usuarios>> GetAllUsuariosAsync() => await _context.usuarios.Include(u => u.Rol).ToListAsync();

        // ============================================================================
        // 🚀 MOTOR DE PAGINACIÓN DE PROVEEDORES
        // ============================================================================
        public async Task<IEnumerable<Usuarios>> GetProveedoresPaginadosAsync(int page, int pageSize, string? search)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _context.usuarios
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => (u.nombre != null && u.nombre.Contains(search)) || 
                                         (u.email != null && u.email.Contains(search)) || 
                                         (u.telefono != null && u.telefono.Contains(search)));
            }

            return await query
                .OrderBy(u => u.nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}