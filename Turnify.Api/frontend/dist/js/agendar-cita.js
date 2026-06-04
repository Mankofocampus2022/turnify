/* ============================================================
   TURNIFY - MOTOR DE AGENDAMIENTO Y DISPONIBILIDAD HORARIA
   ============================================================ */

document.addEventListener('DOMContentLoaded', async () => {
    // 🛡️ [NUEVO] DETECCIÓN DE CÓDIGO QR / AISLAMIENTO DE MULTI-TENANT (Extracción Temprana)
    const urlParams = new URLSearchParams(window.location.search);
    const qrProveedorId = urlParams.get('id');

    // 🛡️ Blindaje de sesión: Soporta múltiples llaves de token por compatibilidad
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const userStr = localStorage.getItem('user');
    
    // 🚀 FIX: Si NO hay QR y NO hay sesión, forzamos login. Si hay QR, habilitamos Guest Checkout.
    if (!qrProveedorId && (!token || !userStr)) {
        window.location.href = 'login.html';
        return;
    }

    // Estructura segura contra nulabilidad en modo incógnito
    const user = userStr ? JSON.parse(userStr) : { id: '00000000-0000-0000-0000-000000000000' };
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();
    const esCliente = rol.includes("CLIENTE");

    if (qrProveedorId) {
        // 🚀 OBSERVACIÓN EXCEL MATADA: Machetazo visual inmediato al Sidebar y Header administrativo
        const sidebar = document.querySelector('.sidebar') || document.getElementById('sidebar');
        const headerAdmin = document.querySelector('.header-admin') || document.querySelector('header');
        const mainContent = document.querySelector('.main-content') || document.querySelector('.wrapper');
        
        if (sidebar) sidebar.style.display = 'none';
        if (headerAdmin) headerAdmin.style.display = 'none';
        
        // Forzamos al formulario a tomar todo el ancho real de la pantalla del celular/PC
        if (mainContent) {
            mainContent.style.marginLeft = '0';
            mainContent.style.width = '100%';
            mainContent.style.padding = '15px';
            mainContent.style.boxShadow = 'none';
        }

        // Si no hay sesión activa, visualizamos el formulario de contacto para invitados
        if (!token) {
            const secAnonimo = document.getElementById('sectionClienteAnonimo');
            if (secAnonimo) secAnonimo.style.display = 'block';
        }
    }

    // 🚩 URL Dinámica: Blindada para Docker y entornos productivos (Matriz de Red Inteligente)
    let API_BASE = window.location.origin + '/api';
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        // Si accedes a través de una IP de Red Local (ej. Pruebas desde el celular), mapea al puerto 5000
        API_BASE = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }

    console.log("🚀 [Turnify Log] Rol Detectado:", rol);

    // 🚩 LÓGICA DE INTERFAZ POR ROL (Mantenemos tu estructura intacta)
    const sectionCliente = document.getElementById('sectionSeleccionarCliente');
    const sectionProveedor = document.getElementById('sectionSeleccionarProveedor');
    const subtitulo = document.getElementById('subtituloAgendar');

    if (esCliente) {
        if (sectionCliente) sectionCliente.style.display = 'none';
        if (sectionProveedor) sectionProveedor.style.display = 'block';
        if (subtitulo) subtitulo.innerText = "Reserva tu cita con tu profesional favorito.";
        cargarProveedores(token, API_BASE); 
        // 🛡️ [NUEVO] Cargamos historial para clientes con el ID correcto de la tabla Clientes
        // 🚩 FIX: Prioridad absoluta al clienteId para evitar el 400 Bad Request
        if (token && (user.clienteId || user.id)) cargarMisCitas(user.clienteId || user.id, token, "cliente", API_BASE);
    } else {
        if (sectionCliente) sectionCliente.style.display = 'block';
        if (sectionProveedor) sectionProveedor.style.display = 'none';
        if (subtitulo) subtitulo.innerText = "Registro manual de servicios (Local / Domicilio)";
        if (token) cargarClientes(token, API_BASE);
        const proveedorId = user.proveedorId || user.id;
        if (token && proveedorId && proveedorId !== '00000000-0000-0000-0000-000000000000') cargarServicios(proveedorId, token, API_BASE);
        // 🛡️ [NUEVO] Cargamos agenda para profesionales
        if (token && proveedorId && proveedorId !== '00000000-0000-0000-0000-000000000000') cargarMisCitas(proveedorId, token, "proveedor", API_BASE);
    }

    // 🛡️ [NUEVO OVERRIDE] Forzar visibilidad de agendamiento si entramos por código QR
    if (qrProveedorId) {
        if (sectionCliente) sectionCliente.style.display = 'none';
        if (sectionProveedor) sectionProveedor.style.display = 'block';
        if (!token && subtitulo) {
            subtitulo.innerText = "Cargando información del establecimiento...";
        }
        
        // Forzamos la carga de proveedores de manera pública (con token nulo o real)
        cargarProveedores(token, API_BASE);
        // Inyectamos de inmediato los servicios del portafolio del QR
        cargarServicios(qrProveedorId, token, API_BASE);
    }

    // 🔥 [KILLER FIX] - VINCULACIÓN DE EVENTOS DE DISPONIBILIDAD
    const inputFecha = document.getElementById('citaFecha');
    const selectServicio = document.getElementById('citaServicioId');
    const selectModalidad = document.getElementById('citaModalidad');

    if (inputFecha) inputFecha.addEventListener('change', () => cargarDisponibilidad(API_BASE));
    if (selectServicio) selectServicio.addEventListener('change', () => cargarDisponibilidad(API_BASE));
    
    if (selectModalidad) {
        selectModalidad.addEventListener('change', () => {
            toggleDireccionCita();
            cargarDisponibilidad(API_BASE);
        });
    }

    const formCita = document.getElementById('formNuevaCita');
    if (formCita) {
        formCita.addEventListener('submit', (e) => guardarCita(e, token, user, rol, API_BASE));
    }
});

// --- [NUEVO] 📜 GESTIÓN DE MIS CITAS (VER, CANCELAR, CAMBIAR) ---
async function cargarMisCitas(id, token, tipo, API_BASE) {
    const container = document.getElementById('listaMisCitas'); 
    if (!container) return;

    console.log(`📡 [Fetch] Obteniendo agenda para ${tipo}...`);
    // 🚩 Blindamos la ruta según el endpoint del CitasController
    const url = tipo === "cliente" ? 
        `${API_BASE}/Citas/historial/${id}` : 
        `${API_BASE}/Citas/hoy`; 

    try {
        const resp = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        
        // 🛡️ BLINDAJE ANTI-CRASH: Validamos si la respuesta es JSON antes de tratar de mapear
        if (resp.ok) {
            const contentType = resp.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                const citas = await resp.json();
                if (Array.isArray(citas)) {
                    renderizarCitas(citas, container, token, API_BASE);
                } else {
                    renderizarCitas([], container, token, API_BASE);
                }
            } else {
                renderizarCitas([], container, token, API_BASE);
            }
        } else {
            renderizarCitas([], container, token, API_BASE);
        }
    } catch (e) { 
        console.log("🔥 Error cargando mis citas:", e);
        container.innerHTML = "<p style='color: #ff5e5e; font-size: 0.7rem;'>Error al cargar agenda.</p>";
    }
}

// 🛡️ REPARACIÓN DE RENDERIZADO: Estilo Glass y Token de Seguridad
function renderizarCitas(citas, container, token, API_BASE) {
    if (!Array.isArray(citas) || citas.length === 0) {
        container.innerHTML = "<p style='color: rgba(255,255,255,0.5); font-size: 0.75rem; padding: 10px;'>No tienes citas registradas.</p>";
        return;
    }

    container.innerHTML = citas.map(c => `
        <div class="card-cita-sidebar ${c.estado.toLowerCase()}">
            ${c.estado === 'pendiente' ? `
                <button class="btn-cancelar-mini" onclick="cancelarCita('${c.id}', '${token}', '${API_BASE}')" title="Cancelar Cita">
                    <i class="fas fa-times-circle"></i>
                </button>
            ` : ''}
            <h6>${c.servicioNombre || "Servicio"}</h6>
            <div class="cita-info">
                <i class="far fa-calendar-alt"></i> ${c.fecha ? c.fecha.split('T')[0] : 'Hoy'}
            </div>
            <div class="cita-info">
                <i class="far fa-clock"></i> ${c.hora ? c.hora.toString().slice(0, 5) : '--:--'}
            </div>
            <div class="cita-info">
                <i class="fas fa-info-circle"></i> ${c.estado ? c.estado.toUpperCase() : 'PENDIENTE'}
            </div>
            ${c.codigoVerificacion ? `
                <div class="token-tag">
                    <i class="fas fa-key"></i> TOKEN: ${c.codigoVerificacion}
                </div>
            ` : ''}
        </div>
    `).join('');
}

// 🛡️ CANCELACIÓN BLINDADA (PATCH al endpoint correcto)
async function cancelarCita(id, token, API_BASE) {
    if (!confirm("⚠️ ¿Estás seguro de que deseas cancelar esta cita?")) return;

    try {
        const resp = await fetch(`${API_BASE}/Citas/${id}/estado`, {
            method: 'PATCH',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify({ nuevoEstado: 'cancelada' })
        });

        if (resp.ok) {
            alert("✅ Cita cancelada correctamente.");
            location.reload(); 
        } else {
            const errText = await resp.text();
            alert("❌ Error: " + errText);
        }
    } catch (e) { console.error("🔥 Error conexión cancelación:", e); }
}

/**
 * 🚩 CARGA DE PROVEEDORES
 */
async function cargarProveedores(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Proveedores`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });
        if (resp.ok) {
            const proveedores = await resp.json();
            const selectProv = document.getElementById('citaProveedorId');
            if (selectProv) {
                selectProv.innerHTML = '<option value="">-- Selecciona Profesional --</option>' + 
                    proveedores.map(p => `<option value="${p.id}">${p.nombreComercial || p.nombre}</option>`).join('');
                
                // 🛡️ [NUEVO] OBSERVACIÓN EXCEL MATADA: Forzamos el aislamiento estricto de negocio de este QR
                const urlParams = new URLSearchParams(window.location.search);
                const qrId = urlParams.get('id');
                if (qrId) {
                    selectProv.value = qrId;
                    selectProv.disabled = true; // Impedimos que el cliente altere el ID o vea otros negocios
                    
                    // Renombramos dinámicamente el título con el nombre de la barbería/manicurista
                    const provSeleccionado = proveedores.find(p => p.id === qrId);
                    if (provSeleccionado && document.getElementById('subtituloAgendar')) {
                        document.getElementById('subtituloAgendar').innerText = `Agendando cita en: ${provSeleccionado.nombreComercial || provSeleccionado.nombre}`;
                    }
                    // Forzamos de inmediato la inyección de sus servicios específicos
                    cargarServicios(qrId, token, API_BASE);
                }

                selectProv.onchange = () => {
                    cargarServiciosPorProveedor(API_BASE);
                    const container = document.getElementById('containerSlots');
                    if (container) container.innerHTML = "";
                };
            }
        }
    } catch (e) { console.error("🔥 Error proveedores:", e); }
}

async function cargarServiciosPorProveedor(API_BASE) {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const selectProv = document.getElementById('citaProveedorId');
    if (!selectProv) return;
    
    const proveedorId = selectProv.value;
    if (proveedorId) {
        cargarServicios(proveedorId, token, API_BASE);
    } else {
        const selectServicio = document.getElementById('citaServicioId');
        if (selectServicio) selectServicio.innerHTML = '<option value="">¿Qué servicio realizaremos?</option>';
    }
}

async function cargarServicios(proveedorId, token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Servicios/proveedor/${proveedorId}`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });

        if (resp.ok) {
            const servicios = await resp.json();
            const selectServicio = document.getElementById('citaServicioId');
            if (selectServicio) {
                selectServicio.innerHTML = '<option value="">Selecciona un servicio</option>' + 
                    servicios.map(s => `<option value="${s.id}">${s.nombre} ($${s.precio})</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error servicios:", e); }
}

async function cargarClientes(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Clientes`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });
        if (resp.ok) {
            const clientes = await resp.json();
            const selectCliente = document.getElementById('citaClienteId');
            if (selectCliente) {
                selectCliente.innerHTML = '<option value="">-- Buscar Cliente --</option>' + 
                    clientes.map(c => `<option value="${c.id}">${c.nombre} (${c.telefono})</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error clientes:", e); }
}

function toggleDireccionCita() {
    const modalidad = document.getElementById('citaModalidad').value;
    const groupDireccion = document.getElementById('groupDireccionCita');
    const inputDireccion = document.getElementById('citaDireccion');

    if (modalidad === 'domicilio') {
        if (groupDireccion) groupDireccion.style.display = 'block';
        if (inputDireccion) inputDireccion.required = true;
    } else {
        if (groupDireccion) groupDireccion.style.display = 'none';
        if (inputDireccion) {
            inputDireccion.required = false;
            inputDireccion.value = ""; 
        }
    }
}

// 🛡️ MOTOR DE DISPONIBILIDAD PRO: Blindado con servicioId
async function cargarDisponibilidad(API_BASE) {
    const fecha = document.getElementById('citaFecha').value;
    const servicioId = document.getElementById('citaServicioId').value;
    const userStr = localStorage.getItem('user');
    
    const urlParamsCheckDisp = new URLSearchParams(window.location.search);
    const qrIdCheckDisp = urlParamsCheckDisp.get('id');

    // 🚀 FIX: Permitimos el cálculo de slots de tiempo si el usuario entra de forma anónima vía QR
    if ((!userStr && !qrIdCheckDisp) || !fecha || !servicioId) return;

    const user = userStr ? JSON.parse(userStr) : { id: '00000000-0000-0000-0000-000000000000' };
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    
    const selectProv = document.getElementById('citaProveedorId');
    
    // 🛡️ [NUEVO] Prioridad absoluta al ID del QR para calcular la agenda de forma aislada
    const urlParams = new URLSearchParams(window.location.search);
    const qrId = urlParams.get('id');

    const proveedorId = qrId || (rol.includes("CLIENTE") ? 
        (selectProv ? selectProv.value : null) : 
        (user.proveedorId || user.id));

    const container = document.getElementById('containerSlots');
    if (!proveedorId) return;

    if (container) container.innerHTML = "<p><i class='fas fa-spinner fa-spin'></i> Calculando túnel de tiempo...</p>";

    try {
        const resp = await fetch(`${API_BASE}/Citas/disponibilidad?proveedorId=${proveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        
        // 🛡️ BLINDAJE ANTI-JSON-ERROR: No intentamos parsear si no es un JSON válido (ej. Error 400 texto)
        const contentType = resp.headers.get("content-type");
        if (resp.ok && contentType && contentType.includes("application/json")) {
            const slots = await resp.json();
            if (container) {
                if(!slots || slots.length === 0) {
                    container.innerHTML = "<p style='color: #e94560;'>Sin disponibilidad.</p>";
                    return;
                }
                container.innerHTML = slots.map(h => 
                    `<div class="slot" onclick="seleccionarHora('${h}', this)">${h.slice(0, 5)}</div>`
                ).join('');
            }
        } else {
            const msgError = await resp.text();
            if (container) container.innerHTML = `<p style='color: #ff5e5e;'>${msgError || "Error de horario"}</p>`;
        }
    } catch (e) { 
        console.error("🔥 Error disponibilidad:", e); 
        if (container) container.innerHTML = "<p style='color: #ff5e5e;'>Error de red.</p>";
    }
}

function seleccionarHora(hora, elemento) {
    document.querySelectorAll('.slot').forEach(s => s.classList.remove('selected'));
    elemento.classList.add('selected');
    const inputHora = document.getElementById('citaHoraSeleccionada');
    if (inputHora) inputHora.value = hora;
}

// 🛡️ GUARDAR CITA: Blindaje de Token e Identidad (Fix "El cliente no existe" y Pruebas QR)
async function guardarCita(e, token, user, rol, API_BASE) {
    e.preventDefault();
    
    const inputHora = document.getElementById('citaHoraSeleccionada');
    let hora = inputHora ? inputHora.value : null;
    
    // 🚀 FIX CRÍTICO: Formateo de hora obligatorio para .NET (HH:mm:ss)
    // Agregamos los segundos si vienen en formato HH:mm para evitar el error 400
    if (hora && hora.length === 5) {
        hora = `${hora}:00`;
    }

    const esCliente = rol.includes("CLIENTE");

    const selectCliente = document.getElementById('citaClienteId');
    const selectProv = document.getElementById('citaProveedorId');
    
    // 🛡️ [NUEVO] Prioridad absoluta al ID del QR en el payload de guardado federado
    const urlParams = new URLSearchParams(window.location.search);
    const qrId = urlParams.get('id');

    // 🚀 [BUG 1 RESTRICTION] - SI UN PROVEEDOR/ADMIN ESCANEA EL QR, NO PUEDE AUTO-AGENDARSE CITAS
    if (token && !esCliente && qrId) {
        return alert("❌ Error de negocio: Un proveedor o administrador no puede solicitar citas para sí mismo. Por favor, cierra sesión o ingresa desde una cuenta de cliente.");
    }

    // 🚀 INYECCIÓN DINÁMICA DE IDENTIDAD:
    // Si viene por QR, el cliente es el usuario autenticado (tú testeando o un cliente real).
    // Si no viene por QR, respetamos tu flujo original (user.clienteId o el select administrativo).
    const clienteIdFinal = qrId ? (user.clienteId || user.id) : (esCliente ? user.clienteId : (selectCliente ? selectCliente.value : null));
    const proveedorIdFinal = qrId || (esCliente ? (selectProv ? selectProv.value : null) : (user.proveedorId || user.id));

    if (esCliente && !user.clienteId && !qrId) {
        return alert("❌ Error de Identidad: No se detectó tu ID de Cliente. Por favor, cierra sesión y vuelve a entrar.");
    }

    if (!clienteIdFinal || !proveedorIdFinal || !hora) return alert("⚠️ Completa todos los campos.");

    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Agendando...';

    const dto = {
        clienteId: clienteIdFinal,
        servicioId: document.getElementById('citaServicioId').value,
        fecha: document.getElementById('citaFecha').value,
        hora: hora, // Ya incluye los segundos gracias al fix
        modalidad: document.getElementById('citaModalidad').value,
        direccion: document.getElementById('citaDireccion').value.trim(),
        observaciones: document.getElementById('citaObservaciones').value.trim(),
        metodoRegistro: qrId ? "QR_Cliente" : (esCliente ? "Panel_Cliente" : "Barbero_Manual"),
        // 🚀 [BUG 2 PAYLOAD] Inyección limpia de propiedades de contacto para la auto-creación en caliente de invitados
        anonimoNombre: document.getElementById('citaAnonimoNombre') ? document.getElementById('citaAnonimoNombre').value.trim() : "",
        anonimoEmail: document.getElementById('citaAnonimoEmail') ? document.getElementById('citaAnonimoEmail').value.trim() : "",
        anonimoWhatsApp: document.getElementById('citaAnonimoWhatsApp') ? document.getElementById('citaAnonimoWhatsApp').value.trim() : ""
    };

    // Validación preventiva en el cliente si es un flujo anónimo sin credenciales
    if (!token && (!dto.anonimoNombre || !dto.anonimoEmail || !dto.anonimoWhatsApp)) {
        btn.disabled = false;
        btn.innerHTML = originalHTML;
        return alert("⚠️ Por favor completa tu Nombre, Correo y WhatsApp en el Paso 1 para poder procesar la reserva.");
    }

    try {
        const resp = await fetch(`${API_BASE}/Citas/agendar`, {
            method: 'POST',
            headers: token ? { 
                'Content-Type': 'application/json', 
                'Authorization': `Bearer ${token}` 
            } : {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(dto)
        });

        // 🛡️ BLINDAJE DE RESPUESTA: Captura errores de texto del servidor
        const contentType = resp.headers.get("content-type");
        if (contentType && contentType.includes("application/json")) {
            const data = await resp.json();
            if (resp.ok) {
                alert(`✅ ¡Cita Confirmada!\n\n${data.message}`); 
                window.location.reload(); 
            } else {
                alert("❌ No se pudo agendar: " + (data.message || "Error desconocido."));
                btn.disabled = false;
                btn.innerHTML = originalHTML;
            }
        } else {
            const errorTexto = await resp.text();
            alert("❌ Error del Servidor: " + errorTexto);
            btn.disabled = false;
            btn.innerHTML = originalHTML;
        }
    } catch (e) { 
        alert("🔌 Error de conexión.");
        btn.disabled = false;
        btn.innerHTML = originalHTML;
    }
}

// PUENTE GLOBAL PARA HTML
window.seleccionarHora = seleccionarHora;
window.cancelarCita = cancelarCita;