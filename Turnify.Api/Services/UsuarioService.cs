using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;

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
                    var existeEmail = await _context.usuarios.AnyAsync(u => u.email == dto.Email);
                    if (existeEmail) 
                    {
                        return (false, "El correo electrónico ya se encuentra registrado.", (Guid?)null);
                    }

                    // 1. CREACIÓN DEL USUARIO BASE
                    var usuario = new Usuarios {
                        id = Guid.NewGuid(),
                        nombre = dto.Nombre,
                        email = dto.Email,
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

                    // 3. ESPECIALIZACIÓN DE PERFIL (Sin columna 'telefono' para evitar el Crash)
                    if (dto.RolId == idCliente) {
                        _context.clientes.Add(new Clientes {
                            id = Guid.NewGuid(),
                            usuario_id = usuario.id,
                            nombre = dto.Nombre,
                            // 🚩 QUITAMOS EL TELÉFONO AQUÍ
                            activo = true,
                            fecha_creacion = DateTime.UtcNow
                        });
                    }
                    else if (dto.RolId == idProveedor) {
                        _context.proveedores.Add(new Proveedores {
                            Id = Guid.NewGuid(),
                            UsuarioId = usuario.id,
                            NombreComercial = dto.NombreComercial ?? $"Barbería de {dto.Nombre}",
                            Tipo = "Barbería",
                            // 🚩 QUITAMOS EL TELÉFONO AQUÍ
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
                    Console.WriteLine($"--- 🚨 ERROR EN REGISTRO: {ex.Message} ---");
                    throw; // Re-lanzamos para que la estrategia de reintento gestione el fallo
                }
            });
        }

        public async Task<(bool Success, string Message, object? Data)> LoginAsync(LoginDto dto) {
            // El login crasheaba porque al incluir el Rol o buscar el Proveedor después, 
            // EF intentaba leer la columna 'telefono' inexistente.
            var u = await _context.usuarios.Include(x => x.Rol).FirstOrDefaultAsync(x => x.email == dto.Email);
            
            if (u == null || !BCrypt.Net.BCrypt.Verify(dto.Password, u.password_hash)) 
                return (false, "Credenciales incorrectas.", null);
                
            return (true, "OK", u);
        }

        public async Task<int> GetTotalUsuariosActivosAsync() {
            // Comparación segura para bool?
            return await _context.usuarios.CountAsync(u => u.activo == true);
        }

        // --- MÉTODOS CRUD (Mantenidos intactos como pediste) ---
        public async Task<Usuarios?> GetUsuarioByIdAsync(Guid id) => await _context.usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.id == id);
        public async Task<bool> ActualizarAsync(Usuarios u) { _context.Entry(u).State = EntityState.Modified; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> EliminarLogicoAsync(Guid id) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.activo = false; return await _context.SaveChangesAsync() > 0; }
        public async Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool b) { var u = await _context.usuarios.FindAsync(id); if (u == null) return false; u.esta_bloqueado = b; return await _context.SaveChangesAsync() > 0; }
        public async Task<IEnumerable<Usuarios>> GetAllUsuariosAsync() => await _context.usuarios.Include(u => u.Rol).ToListAsync();
    }
}