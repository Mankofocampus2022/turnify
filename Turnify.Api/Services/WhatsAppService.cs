using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using System.Text; 

namespace Turnify.Api.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly TurnifyDbContext _context;
        private readonly ILogger<WhatsAppService> _logger;

        // 🧠 MEMORIA VOLÁTIL DEL BOT (Máquina de Estados Concurrente)
        private static readonly ConcurrentDictionary<string, BotSession> _sesionesBot = new();

        public WhatsAppService(TurnifyDbContext context, ILogger<WhatsAppService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 🚩 MÉTODO PRIVADO: Sincronización horaria de Bogotá para validaciones lógicas del Bot
        private DateTime GetBogotaTime()
        {
            try 
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var tzId = isWindows ? "SA Pacific Standard Time" : "America/Bogota";
                var bogotaZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogotaZone);
            }
            catch 
            {
                // Fallback manual UTC-5 si hay restricciones de entorno corporativo
                return DateTime.UtcNow.AddHours(-5);
            }
        }

        private string GenerarTokenCheckInLocal()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; 
            var random = new byte[6];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(random);
            }
            var result = new StringBuilder(6);
            foreach (byte b in random)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }

        // =================================================================
        // 🔔 ENVIAR RECORDATORIO PROACTIVO (Outbound)
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
            Console.WriteLine("[ Button 1: 👍 Confirmar ]  [ Button 2: ❌ Cancelar ]");
            Console.WriteLine("--------------------------------------------------\n");

            return true;
        }

        // =================================================================
        // 🚀 REQUISITO 3: [NUEVO] DESPACHO AUTOMÁTICO DE TOKEN (Outbound Directo)
        // =================================================================
        public async Task<bool> EnviarMensajeTokenAsync(string telefonoCliente, string nombreCliente, string tokenCheckIn, string establecimientoNombre)
        {
            if (string.IsNullOrEmpty(telefonoCliente)) return false;

            _logger.LogInformation($"📥 [WhatsApp API Meta Outbound] Despachando confirmación y token a {telefonoCliente}...");

            Console.WriteLine("\n--------------------------------------------------");
            Console.WriteLine($"📱 NOTIFICACIÓN DE AGENDAMIENTO EXITOSO (WHATSAPP)");
            Console.WriteLine($"Destinatario Celular: {telefonoCliente}");
            Console.WriteLine($"Hola *{nombreCliente}*, ¡tu cita ha sido confirmada en *{establecimientoNombre}*!");
            Console.WriteLine($"🔑 **TU CÓDIGO DE CHECK-IN ES: {tokenCheckIn}**");
            Console.WriteLine($"Presenta este código al llegar al establecimiento para iniciar tu atención. ¡Te esperamos!");
            Console.WriteLine("--------------------------------------------------\n");

            return await Task.FromResult(true);
        }

        // =================================================================
        // 🤖 MOTOR REACTIVO DEL BOT WHATSAPP (Inbound MULTI-TENANT)
        // =================================================================
        public async Task<string> ProcesarMensajeEntranteAsync(string telefonoCliente, string textoMensaje)
        {
            // Sobrecarga pasiva por si se llama al método clásico sin el número del barbero receptor
            return await ProcesarMensajeEntranteAsync(telefonoCliente, null, textoMensaje);
        }

        public async Task<string> ProcesarMensajeEntranteAsync(string telefonoCliente, string telefonoBarberoReceptor, string textoMensaje)
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
                var ahoraBogota = GetBogotaTime();

                // 🏢 DETECCIÓN EN CALIENTE DEL PROVEEDOR MULTI-TENANT (Usando la columna 'Telefono' con T mayúscula)
                Proveedores proveedorContexto = null;
                if (!string.IsNullOrEmpty(telefonoBarberoReceptor))
                {
                    proveedorContexto = await _context.proveedores
                        .FirstOrDefaultAsync(p => p.Telefono == telefonoBarberoReceptor);
                }

                switch (session.PasoActual)
                {
                    case PasoBot.SaludoInicial:
                        session.PasoActual = PasoBot.WaitingOpcionMenu;
                        
                        string saludoPersonalizado = "👋 ¡Hola! Bienvenido al asistente automático de *Turnify* 🗓️.\n\n";
                        if (proveedorContexto != null)
                        {
                            saludoPersonalizado = $"👋 ¡Hola! Bienvenido al asistente de *{proveedorContexto.NombreComercial}* (vía *Turnify*) 💈.\n\n";
                        }

                        return saludoPersonalizado +
                               "¿Cómo te puedo ayudar hoy? Digita el número de la opción:\n" +
                               "1️⃣ *Agendar una nueva cita*\n" +
                               "2️⃣ *Ver mis citas pendientes*\n" +
                               "3️⃣ *Cancelar una cita pendiente* ❌\n\n" +
                               "• Escribe *salir* en cualquier momento para cancelar.";

                    case PasoBot.WaitingOpcionMenu:
                        if (textoMensaje == "1")
                        {
                            // 🚀 ATAJO INTELIGENTE: Si el cliente ya está escribiendo al número exclusivo del barbero, nos saltamos los menús globales
                            if (proveedorContexto != null)
                            {
                                var serviciosDelProveedor = await _context.servicios
                                    .Where(s => s.ProveedorId == proveedorContexto.Id && s.Activo == 1)
                                    .ToListAsync();

                                if (!serviciosDelProveedor.Any())
                                {
                                    session.Reset();
                                    return $"❌ Lo sentimos, *{proveedorContexto.NombreComercial}* no tiene servicios activos configurados en este momento. Inténtalo más tarde.";
                                }

                                session.ProveedorIdSeleccionado = proveedorContexto.Id;
                                session.ProveedorNombreSeleccionado = proveedorContexto.NombreComercial;

                                session.PasoActual = PasoBot.EsperandoServicio;
                                session.MapaOpciones.Clear(); 

                                var menuServiciosDirecto = $"✂️ **Servicios disponibles en {session.ProveedorNombreSeleccionado}:**\n\n";
                                for (int i = 0; i < serviciosDelProveedor.Count; i++)
                                {
                                    menuServiciosDirecto += $"{i + 1}️⃣ *{serviciosDelProveedor[i].Nombre}* (${serviciosDelProveedor[i].Precio})\n";
                                    session.MapaOpciones.TryAdd(i + 1, serviciosDelProveedor[i].Id);
                                }
                                return menuServiciosDirecto;
                            }

                            // Flujo global por defecto si no se detecta número receptor exclusivo
                            session.PasoActual = PasoBot.EsperandoCategoriaServicio;
                            return "💈 **¿Qué tipo de servicio estás buscando hoy?**\n\n" +
                                   "1️⃣ **Barbería** 💈\n" +
                                   "2️⃣ **Manicura / Uñas** 💅\n\n" +
                                   "Digita el número de tu elección:";
                        }
                        else if (textoMensaje == "2")
                        {
                            var citasClienteQuery = _context.citas
                                .Include(c => c.Servicio)
                                .Where(c => c.Cliente != null && c.Cliente.telefono == telefonoCliente && c.Estado == "pendiente");

                            // Si está en el WhatsApp del barbero, solo listamos las citas de ese barbero específico
                            if (proveedorContexto != null)
                            {
                                citasClienteQuery = citasClienteQuery.Where(c => c.ProveedorId == proveedorContexto.Id);
                            }

                            var citasCliente = await citasClienteQuery.ToListAsync();
                            session.Reset(); 

                            if (!citasCliente.Any()) return "🤷‍♂️ No tienes citas pendientes registradas en este chat.";

                            var agendaText = "📅 *Tus próximas citas pendientes:*\n\n";
                            foreach (var c in citasCliente)
                            {
                                agendaText += $"• *{c.Servicio?.Nombre}*: {c.Fecha:dd/MM/yyyy} a las {c.Hora} (Código: {c.CodigoVerificacion})\n";
                            }
                            return agendaText;
                        }
                        else if (textoMensaje == "3")
                        {
                            var citasCancelablesQuery = _context.citas
                                .Include(c => c.Servicio)
                                .Where(c => c.Cliente != null && c.Cliente.telefono == telefonoCliente && c.Estado == "pendiente");

                            if (proveedorContexto != null)
                            {
                                citasCancelablesQuery = citasCancelablesQuery.Where(c => c.ProveedorId == proveedorContexto.Id);
                            }

                            var citasCancelables = await citasCancelablesQuery.ToListAsync();

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
                                session.MapaOpciones.TryAdd(i + 1, citasCancelables[i].Id);
                            }
                            return menuCancelar + "\n❌ Escribe *salir* si cambiaste de opinión.";
                        }
                        return "⚠️ Opción inválida. Responde *1* para agendar, *2* para listar o *3* para cancelar.";

                    case PasoBot.EsperandoCategoriaServicio:
                        string categoriaSeleccionada = "";
                        if (textoMensaje == "1") categoriaSeleccionada = "Barbero";
                        else if (textoMensaje == "2") categoriaSeleccionada = "Manicurista";
                        else return "⚠️ Opción inválida. Responde *1* para Barbería o *2* para Manicura:";

                        var profesionalesDisponibles = await _context.proveedores
                            .Where(p => p.Activo == true && p.Eliminado == false && p.Categoria == categoriaSeleccionada)
                            .Take(5)
                            .ToListAsync();

                        if (!profesionalesDisponibles.Any())
                        {
                            session.Reset();
                            return $"⚠️ Lo sentimos, en este momento no hay profesionales disponibles en *{categoriaSeleccionada}*. Escribe *menu* para reiniciar.";
                        }

                        session.PasoActual = PasoBot.EsperandoProveedor;
                        session.MapaOpciones.Clear();

                        var menuProveedores = $"💈 **Selecciona tu profesional de {categoriaSeleccionada}:**\n\n";
                        for (int i = 0; i < profesionalesDisponibles.Count; i++)
                        {
                            menuProveedores += $"{i + 1}️⃣ **{profesionalesDisponibles[i].NombreComercial}**\n";
                            session.MapaOpciones.TryAdd(i + 1, profesionalesDisponibles[i].Id);
                        }
                        return menuProveedores;

                    case PasoBot.EsperandoProveedor:
                        if (int.TryParse(textoMensaje, out int opcionProv) && session.MapaOpciones.TryGetValue(opcionProv, out Guid proveedorId))
                        {
                            var serviciosDelProveedor = await _context.servicios
                                .Where(s => s.ProveedorId == proveedorId && s.Activo == 1)
                                .ToListAsync();

                            if (!serviciosDelProveedor.Any())
                            {
                                var provErroneo = await _context.proveedores.FindAsync(proveedorId);
                                string catActual = provErroneo?.Categoria ?? "Barbero";

                                var listaReintento = await _context.proveedores
                                    .Where(p => p.Activo == true && p.Eliminado == false && p.Categoria == catActual)
                                    .Take(5)
                                    .ToListAsync();

                                session.MapaOpciones.Clear();
                                var menuReintento = $"❌ **{provErroneo?.NombreComercial ?? "El profesional"}** no tiene servicios configurados en este momento.\n\n" +
                                                     $"💈 **Por favor, selecciona otro profesional disponible de la lista:**\n\n";
                                for (int i = 0; i < listaReintento.Count; i++)
                                {
                                    menuReintento += $"{i + 1}️⃣ **{listaReintento[i].NombreComercial}**\n";
                                    session.MapaOpciones.TryAdd(i + 1, listaReintento[i].Id);
                                }
                                return menuReintento;
                            }

                            session.ProveedorIdSeleccionado = proveedorId;
                            var prov = await _context.proveedores.FindAsync(proveedorId);
                            session.ProveedorNombreSeleccionado = prov?.NombreComercial ?? "Profesional";

                            session.PasoActual = PasoBot.EsperandoServicio;
                            session.MapaOpciones.Clear(); 

                            var menuServicios = $"✂️ **Servicios disponibles con {session.ProveedorNombreSeleccionado}:**\n\n";
                            for (int i = 0; i < serviciosDelProveedor.Count; i++)
                            {
                                menuServicios += $"{i + 1}️⃣ *{serviciosDelProveedor[i].Nombre}* (${serviciosDelProveedor[i].Precio})\n";
                                session.MapaOpciones.TryAdd(i + 1, serviciosDelProveedor[i].Id);
                            }
                            return menuServicios;
                        }
                        return "⚠️ Selección de profesional no válida. Digita un número de la lista.";

                    case PasoBot.EsperandoServicio:
                        if (int.TryParse(textoMensaje, out int opcionService) && session.MapaOpciones.TryGetValue(opcionService, out Guid serviceId))
                        {
                            session.ServicioIdSeleccionado = serviceId;
                            var serv = await _context.servicios.FindAsync(serviceId);
                            session.ServicioNombreSeleccionado = serv?.Nombre ?? "Servicio";

                            session.PasoActual = PasoBot.EsperandoModalidad;
                            session.MapaOpciones.Clear();

                            return $"🎯 Seleccionaste: *{session.ServicioNombreSeleccionado}* con **{session.ProveedorNombreSeleccionado}**.\n\n" +
                                   $"🏢 **¿Dónde deseas recibir el servicio?**\n\n" +
                                   $"1️⃣ **En el local** (Presencial) 🏢\n" +
                                   $"2️⃣ **A domicilio** (En tu ubicación) 🏠\n\n" +
                                   $"Digita el número de tu opción (1 o 2):";
                        }
                        return "⚠️ Selección no válida. Por favor digita el número del servicio de la lista.";

                    case PasoBot.EsperandoModalidad:
                        if (textoMensaje == "1") session.ModalidadSeleccionada = "local";
                        else if (textoMensaje == "2") session.ModalidadSeleccionada = "domicilio";
                        else return "⚠️ Opción inválida. Responde *1* para Local o *2* para Domicilio:";

                        session.PasoActual = PasoBot.EsperandoFecha;
                        return $"📍 Modalidad: *{session.ModalidadSeleccionada.ToUpper()}* registrada.\n\n" +
                               $"📅 *¿Para qué fecha deseas tu cita?*\nEscríbela en formato: *AÑO-MES-DÍA* (Ejemplo: `2026-06-15`)";

                    case PasoBot.EsperandoFecha:
                        if (DateTime.TryParse(textoMensaje, out DateTime fechaSeleccionada))
                        {
                            if (fechaSeleccionada.Date < ahoraBogota.Date)
                                return "❌ No puedes agendar en days pasados. Ingresa una fecha válida (AAAA-MM-DD):";

                            session.FechaSeleccionada = fechaSeleccionada.Date;

                            var jornadaCompleta = new List<TimeSpan>();
                            for (int h = 6; h <= 21; h++) 
                            {
                                jornadaCompleta.Add(new TimeSpan(h, 0, 0));
                            }

                            var citasOcupadas = await _context.citas
                                .Where(c => c.ProveedorId == session.ProveedorIdSeleccionado && 
                                            c.Fecha == fechaSeleccionada.Date && 
                                            c.Estado != "cancelada")
                                .Select(c => c.Hora)
                                .ToListAsync();

                            session.PasoActual = PasoBot.EsperandoHora;
                            session.MapaHoras.Clear(); 

                            var menuHoras = $"🕒 *Turnos para el {fechaSeleccionada:dd/MM/yyyy} con {session.ProveedorNombreSeleccionado}:*\n\n";
                            
                            for (int i = 0; i < jornadaCompleta.Count; i++)
                            {
                                var horaSlot = jornadaCompleta[i];
                                bool estaOcupado = citasOcupadas.Any(ocupado => Math.Abs((ocupado - horaSlot).TotalMinutes) < 45);
                                
                                DateTime auxiliar = DateTime.Today.Add(horaSlot);
                                string horaFormateada = auxiliar.ToString("hh:mm tt"); 
                                int opcionNumero = i + 1;

                                session.MapaHoras.TryAdd(opcionNumero, horaSlot.ToString());

                                if (estaOcupado) menuHoras += $"🛑 [{opcionNumero}] {horaFormateada} *(Ocupado)*\n";
                                else menuHoras += $"👉 [{opcionNumero}] {horaFormateada}\n";
                            }

                            menuHoras += "\nDigita el número de la opción que prefieras:";
                            return menuHoras;
                        }
                        return "⚠️ Formato incorrecto. Usa el orden: *AÑO-MES-DÍA* (Ejemplo: `2026-06-15`):";

                    case PasoBot.EsperandoHora:
                        if (int.TryParse(textoMensaje, out int opcionHora) && 
                            session.MapaHoras.TryGetValue(opcionHora, out string? horaString) && 
                            TimeSpan.TryParse(horaString, out TimeSpan horaSeleccionada))
                        {
                            var limiteInferior = horaSeleccionada.Subtract(TimeSpan.FromMinutes(44));
                            var limiteSuperior = horaSeleccionada.Add(TimeSpan.FromMinutes(44));

                            var yaOcupado = await _context.citas
                                .AnyAsync(c => c.ProveedorId == session.ProveedorIdSeleccionado && 
                                               c.Fecha == session.FechaSeleccionada.Date && 
                                               c.Estado != "cancelada" &&
                                               c.Hora > limiteInferior && 
                                               c.Hora < limiteSuperior);

                            if (yaOcupado) return "🛑 Turno ocupado. Selecciona una opción disponible:";

                            var cliente = await _context.clientes.FirstOrDefaultAsync(c => c.telefono == telefonoCliente);
                            
                            if (cliente == null)
                            {
                                cliente = new Clientes {
                                    id = Guid.NewGuid(),
                                    nombre = $"Cliente WhatsApp ({telefonoCliente})",
                                    telefono = telefonoCliente,
                                    email = $"{telefonoCliente}@turnify.local", 
                                    activo = true,
                                    fecha_creacion = DateTime.UtcNow
                                };
                                _context.clientes.Add(cliente);
                                await _context.SaveChangesAsync();
                            }

                            var servicio = await _context.servicios.FindAsync(session.ServicioIdSeleccionado);
                            if (servicio == null)
                            {
                                session.Reset();
                                return "❌ Servicio no disponible. Escribe *hola* para reiniciar.";
                            }

                            string tokenGenerado = GenerarTokenCheckInLocal();

                            var nuevaCita = new Citas
                            {
                                Id = Guid.NewGuid(),
                                ClienteId = cliente.id,
                                ProveedorId = session.ProveedorIdSeleccionado,
                                ServicioId = session.ServicioIdSeleccionado,
                                Fecha = session.FechaSeleccionada.Date,
                                Hora = horaSeleccionada,
                                Modalidad = session.ModalidadSeleccionada, 
                                Estado = "pendiente",
                                PrecioPactado = servicio.Precio,
                                DuracionPactadaMin = servicio.DuracionMinutos,
                                FechaCreacion = DateTime.UtcNow,
                                MetodoRegistro = "WhatsApp",
                                CodigoVerificacion = tokenGenerado
                            };

                            _context.citas.Add(nuevaCita);
                            await _context.SaveChangesAsync();

                            Console.WriteLine("\n--------------------------------------------------");
                            Console.WriteLine($"📧 [Email Outbound Broker] Destinatario: {cliente.email} | Token: {tokenGenerado}");
                            Console.WriteLine($"📱 [WhatsApp Outbound Broker] Celular: {telefonoCliente} | Token: {tokenGenerado}");
                            Console.WriteLine("--------------------------------------------------\n");

                            string provFinal = session.ProveedorNombreSeleccionado;
                            string servFinal = session.ServicioNombreSeleccionado;
                            string modFinal = session.ModalidadSeleccionada.ToUpper();
                            string horaFinalLegible = DateTime.Today.Add(horaSeleccionada).ToString("hh:mm tt");

                            await EnviarMensajeTokenAsync(telefonoCliente, cliente.nombre, tokenGenerado, provFinal);

                            session.Reset(); 

                            return $"🎉 ¡Espectacular! Tu cita ha sido agendada con éxito para el día *{nuevaCita.Fecha:dd/MM/yyyy}* bién coordinado a las *{horaFinalLegible}*.\n\n" +
                                   $"💇‍♂️ Profesional: **{provFinal}**\n" +
                                   $"✂️ Servicio: *{servFinal}*\n" +
                                   $"📍 Modalidad: *{modFinal}*\n" +
                                   $"🔑 **TU CÓDIGO DE CONFIRMACIÓN WHATSAPP ES: {tokenGenerado}**\n\n" +
                                   $"📧 También enviamos el respaldo de tu check-in al correo registrado: *{cliente.email}*.\n\n" +
                                   $"¡Gracias por elegir Turnify! 🚀";
                        }
                        return "⚠️ Selección de turno inválida. Digita un número de opción de la lista.";

                    case PasoBot.EsperandoCitaACancelar:
                        if (int.TryParse(textoMensaje, out int opcionCita) && session.MapaOpciones.TryGetValue(opcionCita, out Guid citaId))
                        {
                            var cita = await _context.citas.FindAsync(citaId);
                            if (cita != null)
                            {
                                cita.Estado = "cancelada";
                                cita.Observaciones = "Cancelada automáticamente por el cliente a través del Bot de WhatsApp.";
                                await _context.SaveChangesAsync();
                            }
                            session.Reset(); 
                            return "❌ *Tu cita ha sido cancelada con éxito.*\n\n" +
                                   "¿Deseas reprogramar? Escribe de nuevo *hola* y selecciona la opción 1️⃣.";
                        }
                        return "⚠️ Selección inválida. Digita el número de la cita de la lista.";

                    default:
                        session.Reset();
                        return "👋 Escribe *hola* para iniciar.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [WhatsApp Bot Error] Fallo crítico: {ex.Message}");
                session.Reset();
                return "💥 Tuvimos un inconveniente procesando tu solicitud en el servidor. Escribe *hola* para reintentar.";
            }
        }
    }

    public enum PasoBot { SaludoInicial, WaitingOpcionMenu, EsperandoCategoriaServicio, EsperandoProveedor, EsperandoServicio, EsperandoModalidad, EsperandoFecha, EsperandoHora, EsperandoCitaACancelar }

    public class BotSession
    {
        public PasoBot PasoActual { get; set; } = PasoBot.SaludoInicial;
        public Guid ServicioIdSeleccionado { get; set; }
        public DateTime FechaSeleccionada { get; set; }
        public ConcurrentDictionary<int, Guid> MapaOpciones { get; set; } = new();
        public ConcurrentDictionary<int, string> MapaHoras { get; set; } = new();
        public Guid ProveedorIdSeleccionado { get; set; }
        public string ProveedorNombreSeleccionado { get; set; } = string.Empty;
        public string ServicioNombreSeleccionado { get; set; } = string.Empty;
        public string ModalidadSeleccionada { get; set; } = "local";

        public void Reset()
        {
            PasoActual = PasoBot.SaludoInicial;
            ServicioIdSeleccionado = Guid.Empty;
            FechaSeleccionada = DateTime.MinValue;
            MapaOpciones.Clear();
            MapaHoras.Clear(); 
            ProveedorIdSeleccionado = Guid.Empty;
            ProveedorNombreSeleccionado = string.Empty;
            ServicioNombreSeleccionado = string.Empty;
            ModalidadSeleccionada = "local";
        }
    }
}