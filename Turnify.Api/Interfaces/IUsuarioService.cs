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

        // 🚀 [NUEVO] Firma matriculada para mitigar la OBS-01 (Paginación de Proveedores)
        // Apaga por completo el error CS1061 en el ProveedoresController al compilar
        Task<IEnumerable<Usuarios>> GetProveedoresPaginadosAsync(int page, int pageSize, string? search);
    }
}