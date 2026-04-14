using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IUsuarioService
    {
        Task<(bool Success, string Message, Guid? UsuarioId)> RegistrarAsync(UsuarioRegistroDTO dto);
        Task<(bool Success, string Message, object? Data)> LoginAsync(LoginDto dto);
        Task<Usuarios?> GetUsuarioByIdAsync(Guid id);
        Task<bool> ActualizarAsync(Usuarios usuario);
        Task<bool> EliminarLogicoAsync(Guid id);
        Task<int> GetTotalUsuariosActivosAsync();
        Task<bool> CambiarEstadoBloqueoAsync(Guid id, bool bloquear);
        Task<IEnumerable<Usuarios>> GetAllUsuariosAsync();
    }
}