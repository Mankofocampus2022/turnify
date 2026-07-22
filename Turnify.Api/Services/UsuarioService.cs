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
            // 🛡️ Estrategia de ejecución para manejar reintentos en SQL (Manteniendo tu lógica)
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

                    // 2. IDENTIFICACIÓN DE ROLES (GUIDs oficiales)
                    var idCliente = Guid.Parse("56992f75-6420-4d55-a5f9-9223248c50d7");
                    var idProveedor = Guid.Parse("8854c07c-6e5e-4876-a29a-c7ad5dcfbab7");

                    // 3. ESPECIALIZACIÓN DE PERFIL (Blindaje contra nulos y sincronización dual)
                    if (dto.RolId == idCliente) {
                        _context.clientes.Add(new Clientes {
                            id = Guid.NewGuid(),
                            usuario_id = usuario.id,
                            nombre = string.IsNullOrEmpty(usuario.nombre) ? "Cliente" : usuario.nombre,
                            telefono = telefonoLimpio,
                            email = emailNormalizado, // Sincronizado       
                            activo = true,
                            fecha_creacion = DateTime.UtcNow
                        });
                    }
                    else if (dto.RolId == idProveedor) {
                        _context.proveedores.Add(new Proveedores {
                            Id = Guid.NewGuid(),
                            UsuarioId = usuario.id,
                            NombreComercial = dto.NombreComercial ?? $"Barbería de {(string.IsNullOrEmpty(usuario.nombre) ? "Usuario" : usuario.nombre)}",
                            Tipo = dto.TipoNegocio ?? "Barbería",
                            // 🚩 KILLER FIX: Guardamos el email en la tabla proveedores para la Validación Dual
                            Email = emailNormalizado, 
                            Telefono = telefonoLimpio,
                            Activo = true,
                            FechaCreacion = DateTime.UtcNow,
                            Direccion = "Pendiente de configuración"
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

        // --- MÉTODOS CRUD (Manteniendo tu estructura original intacta) ---
        public async Task<Usuarios?> GetUsuarioByIdAsync(Guid id) => await _context.usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.id == id);
        public async Task<bool> ActualizarAsync(Usuarios u) { _context.Entry(u).State = EntityState.Modified; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> EliminarLogicoAsync(Guid id) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.activo = false; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool b) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.esta_bloqueado = b; return await _context.SaveChangesAsync() > 0; }
        public async Task<IEnumerable<Usuarios>> GetAllUsuariosAsync() => await _context.usuarios.Include(u => u.Rol).ToListAsync();

        // ============================================================================
        // 🚀 [NUEVO] MOTOR DE PAGINACIÓN DE PROVEEDORES (Mitigación OBS-01)
        // ============================================================================
        public async Task<IEnumerable<Usuarios>> GetProveedoresPaginadosAsync(int page, int pageSize, string? search)
        {
            // Control defensivo para evitar desbordamientos de paginación nula o negativa
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _context.usuarios
                .AsNoTracking() // 🛡️ Bloquea el rastreo en RAM, optimizando el rendimiento en Docker
                .AsQueryable();

            // Filtrar opcionalmente por nombre, correo o teléfono si viene un criterio en el buscador (Ajuste Nulabilidad CS8602)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => (u.nombre != null && u.nombre.Contains(search)) || 
                                         (u.email != null && u.email.Contains(search)) || 
                                         (u.telefono != null && u.telefono.Contains(search)));
            }

            // SQL Server exige obligatoriamente un OrderBy antes de aplicar las cláusulas OFFSET y FETCH NEXT (Skip/Take)
            return await query
                .OrderBy(u => u.nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}