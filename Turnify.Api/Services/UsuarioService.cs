using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using BCrypt.Net;

namespace Turnify.Api.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly TurnifyDbContext _context;

        public UsuarioService(TurnifyDbContext context)
        {
            _context = context;
        }

        // 1. REGISTRO
        public async Task<(bool Success, string Message, Guid? UsuarioId)> RegistrarAsync(Usuarios usuario)
        {
            try
            {
                if (usuario.rol_id == Guid.Empty)
                    return (false, "Error: El rol_id no llegó correctamente.", null);

                var existeEmail = await _context.usuarios.AnyAsync(u => u.email == usuario.email);
                if (existeEmail)
                    return (false, "El correo electrónico ya está registrado.", null);

                usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(usuario.password_hash);

                if (usuario.id == Guid.Empty) usuario.id = Guid.NewGuid();
                usuario.fecha_creacion = DateTime.UtcNow;
                usuario.activo = true;
                usuario.suscripcion_fin = DateTime.UtcNow.AddDays(30); 
                usuario.esta_bloqueado = false;

                _context.usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                return (true, "Usuario registrado con éxito", usuario.id);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        // 2. LOGIN (Blindado contra NULLs)
        public async Task<(bool Success, string Message, object? Data)> LoginAsync(LoginDto loginDto)
        {
            var usuario = await _context.usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.email == loginDto.Email);

            if (usuario == null) return (false, "Usuario no encontrado", null);

            bool passwordValida = BCrypt.Net.BCrypt.Verify(loginDto.Password, usuario.password_hash);
            if (!passwordValida) return (false, "Contraseña incorrecta", null);

            if (usuario.esta_bloqueado == true) 
                return (false, "Tu cuenta ha sido suspendida. Contacta al administrador.", null);

            if (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value < DateTime.UtcNow)
                return (false, "Tu suscripción ha vencido. Es necesario renovar tu plan.", null);

            usuario.ultima_conexion = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (true, "Login exitoso", usuario);
        }

        // 3. OBTENER TODOS (SuperAdmin)
        public async Task<IEnumerable<Usuarios>> GetAllUsuariosAsync() 
        {
            return await _context.usuarios
                .Include(u => u.Rol)
                .ToListAsync();
        }

        // 4. BLOQUEO / ACTIVACIÓN
        public async Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool bloquear)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return false;
            
            usuario.esta_bloqueado = bloquear;
            return await _context.SaveChangesAsync() > 0;
        }

        // 5. MÉTODOS CRUD EXTRAS
        public async Task<Usuarios?> GetUsuarioByIdAsync(Guid id)
        {
            return await _context.usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.id == id);
        }

        public async Task<bool> ActualizarAsync(Usuarios usuario)
        {
            _context.Entry(usuario).State = EntityState.Modified;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> EliminarLogicoAsync(Guid id)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return false;
            
            usuario.activo = false;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<int> GetTotalUsuariosActivosAsync()
        {
            return await _context.usuarios.CountAsync(u => u.activo == true);
        }       
    }
}