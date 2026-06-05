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

        // 🚀 REQUISITO 3: [NUEVO CANAL INTEGRADO] - Despacho Automático de Token de Check-in
        // Envía de forma nativa el código de 6 caracteres al cliente inmediatamente después de agendar
        Task<bool> EnviarMensajeTokenAsync(string telefonoCliente, string nombreCliente, string tokenCheckIn, string establecimientoNombre);
    }
}