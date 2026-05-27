using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Turnify.Api.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly TurnifyDbContext _context;
        private readonly ILogger<WhatsAppService> _logger;

        // 🧠 MEMORIA VOLÁTIL DEL BOT (Máquina de Estados)
        // Guarda temporalmente en qué paso va cada número de teléfono (+57322...)
        private static readonly ConcurrentDictionary<string, BotSession> _sesionesBot = new();

        public WhatsAppService(TurnifyDbContext context, ILogger<WhatsAppService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =================================================================
        // 🛡️ REQUISITO 1: RECORDATORIO PROACTIVO (DÍA ANTERIOR)
        // =================================================================
        public async Task<bool> EnviarRecordatorioCitaAsync(Guid citaId)
        {
            var cita = await _context.citas
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .FirstOrDefaultAsync(c => c.Id == citaId);

            if (cita == null || cita.Cliente == null || cita.Servicio == null)
            {
                _logger.LogWarning($"⚠️ [WhatsApp Outbound] No se pudo enviar recordatorio. Cita {citaId} incompleta.");
                return false;
            }

            _logger.LogInformation($"📥 [WhatsApp API Meta SIMULATION] Sending template to {cita.Cliente.telefono}...");
            
            Console.WriteLine("\n--------------------------------------------------");
            Console.WriteLine($"📱 NOTIFICACIÓN WHATSAPP ENVIADA A: {cita.Cliente.telefono}");
            Console.WriteLine($"Hola {cita.Cliente.nombre}, te recordamos tu cita para el día mañana {cita.Fecha:dd/MM/yyyy} a las {cita.Hora}.");
            Console.WriteLine($"Servicio: {cita.Servicio.Nombre} | Valor: ${cita.PrecioPactado}");
            Console.WriteLine("Por favor confirma tu asistencia interactuando con los botones:");
            Console.WriteLine("[ Button 1: 👍 Confirmar Asistencia ]  [ Button 2: ❌ Cancelar Cita ]");
            Console.WriteLine("--------------------------------------------------\n");

            return true;
        }

        // =================================================================
        // 🛡️ REQUISITO 2: BOT REACTIVO (PEDIR / CANCELAR CITAS)
        // =================================================================
        public async Task<string> ProcesarMensajeEntranteAsync(string telefonoCliente, string textoMensaje)
        {
            textoMensaje = textoMensaje.Trim().ToLower();
            
            var session = _sesionesBot.GetOrAdd(telefonoCliente, _ => new BotSession());

            if (textoMensaje == "salir" || textoMensaje == "reiniciar" || textoMensaje == "menu")
            {
                session.Reset();
                return "🔄 Flujo reiniciado. Escribe *hola* para ver el menú principal.";
            }

            try
            {
                switch (session.PasoActual)
                {
                    case PasoBot.SaludoInicial:
                        session.PasoActual = PasoBot.EsperandoOpcionMenu;
                        // 🚩 MODIFICADO: Añadimos la opción 3 al menú del Bot
                        return "👋 ¡Hola! Bienvenido al asistente automático de *Turnify* 🗓️.\n\n" +
                               "¿Cómo te puedo ayudar hoy? Digita el número de la opción:\n" +
                               "1️⃣ *Agendar una nueva cita*\n" +
                               "2️⃣ *Ver mis citas pendientes*\n" +
                               "3️⃣ *Cancelar una cita pendiente* ❌\n\n" +
                               "• Escribe *salir* en cualquier momento para cancelar.";

                    case PasoBot.EsperandoOpcionMenu:
                        if (textoMensaje == "1")
                        {
                            var serviciosDisponibles = await _context.servicios.Where(s => s.Activo == 1).ToListAsync();
                            if (!serviciosDisponibles.Any())
                            {
                                session.Reset();
                                return "⚠️ Lo sentimos, en este momento no hay servicios configurados en el sistema.";
                            }

                            session.PasoActual = PasoBot.EsperandoServicio;
                            var menuServicios = "✂️ *Selecciona el servicio que deseas:*\n\n";
                            for (int i = 0; i < serviciosDisponibles.Count; i++)
                            {
                                menuServicios += $"{i + 1}️⃣ *{serviciosDisponibles[i].Nombre}* (${serviciosDisponibles[i].Precio})\n";
                                session.MapaOpciones.TryAdd(i + 1, serviciosDisponibles[i].Id);
                            }
                            return menuServicios;
                        }
                        else if (textoMensaje == "2")
                        {
                            var citasCliente = await _context.citas
                                .Include(c => c.Servicio)
                                .Where(c => c.Cliente != null && c.Cliente.telefono == telefonoCliente && c.Estado == "pendiente")
                                .ToListAsync();

                            session.Reset(); 

                            if (!citasCliente.Any()) return "🤷‍♂️ No tienes citas pendientes registradas con este número de teléfono.";

                            var agendaText = "📅 *Tus próximas citas pendientes:*\n\n";
                            foreach (var c in citasCliente)
                            {
                                agendaText += $"• *{c.Servicio?.Nombre}*: {c.Fecha:dd/MM/yyyy} a las {c.Hora} (Código: {c.CodigoVerificacion})\n";
                            }
                            return agendaText;
                        }
                        // 🚀 NUEVA RAMA: Flujo de Cancelación Automática desde el Bot
                        else if (textoMensaje == "3")
                        {
                            var citasCancelables = await _context.citas
                                .Include(c => c.Servicio)
                                .Where(c => c.Cliente != null && c.Cliente.telefono == telefonoCliente && c.Estado == "pendiente")
                                .ToListAsync();

                            if (!citasCancelables.Any())
                            {
                                session.Reset();
                                return "🤷‍♂️ No encontramos citas pendientes asociadas a este número que se puedan cancelar.";
                            }

                            session.PasoActual = PasoBot.EsperandoCitaACancelar;
                            var menuCancelar = "🗑️ *Selecciona el número de la cita que deseas CANCELAR:*\n\n";
                            for (int i = 0; i < citasCancelables.Count; i++)
                            {
                                menuCancelar += $"{i + 1}️⃣ *{citasCancelables[i].Servicio?.Nombre}* para el día {citasCancelables[i].Fecha:dd/MM/yyyy} a las {citasCancelables[i].Hora}\n";
                                // Guardamos el Id real de la cita amarrado al índice numérico
                                session.MapaOpciones.TryAdd(i + 1, citasCancelables[i].Id);
                            }
                            return menuCancelar + "\n❌ Escribe *salir* si cambiaste de opinión.";
                        }
                        return "⚠️ Opción inválida. Responde *1* para agendar, *2* para listar o *3* para cancelar.";

                    case PasoBot.EsperandoServicio:
                        if (int.TryParse(textoMensaje, out int opcionServicio) && session.MapaOpciones.TryGetValue(opcionServicio, out Guid servicioId))
                        {
                            session.ServicioIdSeleccionado = servicioId;
                            session.PasoActual = PasoBot.EsperandoFecha;
                            session.MapaOpciones.Clear(); 

                            return "📅 *¿Para qué fecha deseas tu cita?*\nPor favor escríbela en formato: *AÑO-MES-DÍA* (Ejemplo: `2026-05-28`)";
                        }
                        return "⚠️ Selección no válida. Por favor digita el número del servicio de la lista.";

                    case PasoBot.EsperandoFecha:
                        if (DateTime.TryParse(textoMensaje, out DateTime fechaSeleccionada))
                        {
                            if (fechaSeleccionada.Date < DateTime.Today)
                            {
                                return "❌ No puedes agendar en días pasados. Ingresa una fecha válida (Formato: `AAAA-MM-DD`):";
                            }

                            session.FechaSeleccionada = fechaSeleccionada.Date;
                            session.PasoActual = PasoBot.EsperandoHora;

                            return $"🕒 *Slots disponibles para el {fechaSeleccionada:dd/MM/yyyy}:*\n" +
                                   "1️⃣ 08:00 AM\n2️⃣ 09:00 AM\n3️⃣ 10:00 AM\n4️⃣ 02:00 PM\n5️⃣ 04:00 PM\n\n" +
                                   "Digita el número de la hora que prefieras:";
                        }
                        return "⚠️ Formato de fecha incorrecto. Recuerda usar el orden: *AÑO-MES-DÍA* (Ejemplo: `2026-05-28`):";

                    case PasoBot.EsperandoHora:
                        var horasMapeadas = new Dictionary<string, TimeSpan> {
                            { "1", new TimeSpan(8, 0, 0) }, { "2", new TimeSpan(9, 0, 0) },
                            { "3", new TimeSpan(10, 0, 0) }, { "4", new TimeSpan(14, 0, 0) },
                            { "5", new TimeSpan(16, 0, 0) }
                        };

                        if (horasMapeadas.TryGetValue(textoMensaje, out TimeSpan horaSeleccionada))
                        {
                            session.Reset(); 
                            return $"🎉 ¡Espectacular! Tu cita ha sido pre-agendada con éxito para el día *{session.FechaSeleccionada:dd/MM/yyyy}* a las *{horaSeleccionada}*.\n" +
                                   "Pronto recibirás el código de confirmación en este chat. ¡Gracias por usar Turnify! 🚀";
                        }
                        return "⚠️ Selección de hora inválida. Elige un número del 1 al 5.";

                    // 🚀 NUEVO ESTADO: PROCESAR LA CANCELACIÓN FÍSICA EN DB
                    case PasoBot.EsperandoCitaACancelar:
                        if (int.TryParse(textoMensaje, out int opcionCita) && session.MapaOpciones.TryGetValue(opcionCita, out Guid citaId))
                        {
                            var cita = await _context.citas.FindAsync(citaId);
                            if (cita != null)
                            {
                                // Cambiamos el estado respetando la columna row_version que blindamos hoy
                                cita.Estado = "cancelada";
                                cita.Observaciones = string.IsNullOrEmpty(cita.Observaciones)
                                    ? "Cancelada de forma automática por el cliente a través del Bot de WhatsApp."
                                    : $"{cita.Observaciones} | Cancelada por el Bot de WhatsApp.";

                                await _context.SaveChangesAsync();
                            }

                            session.Reset(); // Limpiamos la memoria del bot
                            
                            // 🔄 El enganche perfecto: Le confirmamos la cancelación y lo invitamos estratégicamente a reprogramar de una vez
                            return "❌ *Tu cita ha sido cancelada con éxito.*\n\n" +
                                   "¿Deseas reprogramar o agendar un nuevo espacio? ¡Es muy fácil! Escribe de nuevo *hola* y selecciona la opción 1️⃣.";
                        }
                        return "⚠️ Selección inválida. Por favor digita el número de la lista correspondiente a la cita que deseas tumbar.";

                    default:
                        session.Reset();
                        return "👋 Escribe *hola* para iniciar.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [WhatsApp Bot Error] Fallo crítico: {ex.Message}");
                session.Reset();
                return "💥 Ups, tuvimos un inconveniente procesando tu solicitud en el servidor. Por favor intenta de nuevo escribiendo *hola*.";
            }
        }
    }

    // =================================================================
    // 🧠 ENTIDADES AUXILIARES PARA EL CONTROL DE ESTADOS
    // =================================================================
    // 🚩 MODIFICADO: Agregamos 'EsperandoCitaACancelar' al listado de estados
    public enum PasoBot { SaludoInicial, EsperandoOpcionMenu, EsperandoServicio, EsperandoFecha, EsperandoHora, EsperandoCitaACancelar }

    public class BotSession
    {
        public PasoBot PasoActual { get; set; } = PasoBot.SaludoInicial;
        public Guid ServicioIdSeleccionado { get; set; }
        public DateTime FechaSeleccionada { get; set; }
        public System.Collections.Concurrent.ConcurrentDictionary<int, Guid> MapaOpciones { get; set; } = new();

        public void Reset()
        {
            PasoActual = PasoBot.SaludoInicial;
            ServicioIdSeleccionado = Guid.Empty;
            FechaSeleccionada = DateTime.MinValue;
            MapaOpciones.Clear();
        }
    }
}