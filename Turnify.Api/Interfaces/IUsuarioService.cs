using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IUsuarioService
    {
        Task<(bool Success, string Message, object? Data)> LoginAsync(LoginDto dto);
        Task<(bool Success, string Message, Guid? UsuarioId)> RegistrarAsync(Usuarios nuevoUsuario);
        
        Task<Usuarios?> GetUsuarioByIdAsync(Guid id);
        Task<bool> ActualizarAsync(Usuarios usuario);
        Task<bool> EliminarLogicoAsync(Guid id);

        Task<int> GetTotalUsuariosActivosAsync();

        // 🔥 Bloquear/Desbloquear para el SuperAdmin
        Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool bloquear);

        // 🔥 Listar todos para el Dashboard
        Task<IEnumerable<Usuarios>> GetAllUsuariosAsync();
    }
}