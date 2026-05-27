using System;
using System.Threading.Tasks;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IWhatsAppService
    {
        // 🛡️ REQUISITO 1: Flujo Proactivo (Outbound)
        // Envía el recordatorio automatizado el día anterior con los botones de confirmación de Meta
        Task<bool> EnviarRecordatorioCitaAsync(Guid citaId);

        // 🛡️ REQUISITO 2: Flujo Reactivo (Inbound - El Bot)
        // Recibe el JSON plano que nos envía el Webhook de Meta/Twilio cuando el usuario interactúa
        Task<string> ProcesarMensajeEntranteAsync(string telefonoCliente, string textoMensaje);
    }
}