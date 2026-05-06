document.addEventListener('DOMContentLoaded', async () => {
    // 🛡️ Blindaje de sesión
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const userStr = localStorage.getItem('user');
    
    if (!token || !userStr) {
        window.location.href = 'login.html';
        return;
    }

    const user = JSON.parse(userStr);
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();
    const esCliente = rol.includes("CLIENTE");

    console.log("🚀 [Turnify Log] Rol Detectado:", rol);

    // 🚩 LÓGICA DE INTERFAZ POR ROL
    const sectionCliente = document.getElementById('sectionSeleccionarCliente');
    const sectionProveedor = document.getElementById('sectionSeleccionarProveedor');
    const subtitulo = document.getElementById('subtituloAgendar');

    if (esCliente) {
        if (sectionCliente) sectionCliente.style.display = 'none';
        if (sectionProveedor) sectionProveedor.style.display = 'block';
        if (subtitulo) subtitulo.innerText = "Reserva tu cita con tu profesional favorito.";
        cargarProveedores(token); 
    } else {
        if (sectionCliente) sectionCliente.style.display = 'block';
        if (sectionProveedor) sectionProveedor.style.display = 'none';
        if (subtitulo) subtitulo.innerText = "Registro manual de servicios (Local / Domicilio)";
        cargarClientes(token);
        const proveedorId = user.proveedorId || user.id;
        cargarServicios(proveedorId, token);
    }

    // 🔥 [KILLER FIX] - VINCULACIÓN DE EVENTOS DE DISPONIBILIDAD
    // Estos eventos "despiertan" la búsqueda de horarios cada vez que algo cambia
    const inputFecha = document.getElementById('citaFecha');
    const selectServicio = document.getElementById('citaServicioId');
    const selectModalidad = document.getElementById('citaModalidad');

    if (inputFecha) inputFecha.addEventListener('change', cargarDisponibilidad);
    if (selectServicio) selectServicio.addEventListener('change', cargarDisponibilidad);
    
    // Vinculamos la modalidad para el tema de domicilios
    if (selectModalidad) {
        selectModalidad.addEventListener('change', () => {
            toggleDireccionCita();
            cargarDisponibilidad();
        });
    }

    // Evento de guardado
    const formCita = document.getElementById('formNuevaCita');
    if (formCita) {
        formCita.addEventListener('submit', (e) => guardarCita(e, token, user, rol));
    }
});

/**
 * 🚩 CARGA DE PROVEEDORES
 */
async function cargarProveedores(token) {
    console.log("📡 [Fetch] Cargando lista de Proveedores...");
    try {
        const resp = await fetch(`http://localhost:5000/api/Proveedores`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (resp.ok) {
            const proveedores = await resp.json();
            const selectProv = document.getElementById('citaProveedorId');
            if (selectProv) {
                selectProv.innerHTML = '<option value="">-- Selecciona Profesional --</option>' + 
                    proveedores.map(p => `<option value="${p.id}">${p.nombreComercial || p.nombre}</option>`).join('');
                
                selectProv.onchange = () => {
                    console.log("🖱️ [Event] Cambio detectado en Profesional");
                    cargarServiciosPorProveedor();
                    // Limpiamos slots al cambiar de barbero
                    const container = document.getElementById('containerSlots');
                    if (container) container.innerHTML = "";
                };
            }
        }
    } catch (e) { console.error("🔥 Error proveedores:", e); }
}

async function cargarServiciosPorProveedor() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const selectProv = document.getElementById('citaProveedorId');
    if (!selectProv) return;
    
    const proveedorId = selectProv.value;
    if (proveedorId) {
        cargarServicios(proveedorId, token);
    } else {
        const selectServicio = document.getElementById('citaServicioId');
        if (selectServicio) selectServicio.innerHTML = '<option value="">¿Qué servicio realizaremos?</option>';
    }
}

async function cargarServicios(proveedorId, token) {
    try {
        const resp = await fetch(`http://localhost:5000/api/Servicios/proveedor/${proveedorId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (resp.ok) {
            const servicios = await resp.json();
            const selectServicio = document.getElementById('citaServicioId');
            if (selectServicio) {
                if (servicios.length === 0) {
                    selectServicio.innerHTML = '<option value="">Sin servicios disponibles</option>';
                } else {
                    selectServicio.innerHTML = '<option value="">Selecciona un servicio</option>' + 
                        servicios.map(s => `<option value="${s.id}">${s.nombre} ($${s.precio})</option>`).join('');
                }
            }
        }
    } catch (e) { console.error("🔥 Error servicios:", e); }
}

async function cargarClientes(token) {
    try {
        const resp = await fetch(`http://localhost:5000/api/Clientes`, {
            headers: { 'Authorization': `Bearer ${token}` }
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

// 🕒 [REPARADO] - CARGAR DISPONIBILIDAD CON BLINDAJE DE DATOS
async function cargarDisponibilidad() {
    const fecha = document.getElementById('citaFecha').value;
    const servicioId = document.getElementById('citaServicioId').value;
    const userStr = localStorage.getItem('user');
    if (!userStr) return;

    const user = JSON.parse(userStr);
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    
    const selectProv = document.getElementById('citaProveedorId');
    const proveedorId = rol.includes("CLIENTE") ? 
        (selectProv ? selectProv.value : null) : 
        (user.proveedorId || user.id);

    // 🛡️ BLINDAJE: Si falta algún dato, no disparamos el fetch pero limpiamos el mensaje anterior
    const container = document.getElementById('containerSlots');
    if (!fecha || !servicioId || !proveedorId) {
        if (container) container.innerHTML = "<p style='color: #48c1b5;'>Por favor selecciona fecha y servicio.</p>";
        return;
    }

    console.log("📅 [Consultando Disponibilidad] Fecha:", fecha, "| Serv:", servicioId, "| Prov:", proveedorId);
    if (container) container.innerHTML = "<p style='color: #48c1b5;'><i class='fas fa-spinner fa-spin'></i> Consultando agenda...</p>";

    try {
        // 🚩 LLAMADA AL API BLINDADA
        const resp = await fetch(`http://localhost:5000/api/Citas/disponibilidad?proveedorId=${proveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        if (resp.ok) {
            const slots = await resp.json();
            if (container) {
                if(!slots || slots.length === 0) {
                    container.innerHTML = "<p style='color: #e94560;'>Sin disponibilidad para este día.</p>";
                    return;
                }
                // Renderizamos los slots
                container.innerHTML = slots.map(h => 
                    `<div class="slot" onclick="seleccionarHora('${h}', this)">${h.slice(0, 5)}</div>`
                ).join('');
            }
        } else {
            if (container) container.innerHTML = "<p style='color: #e94560;'>Error al consultar horarios.</p>";
        }
    } catch (e) { 
        console.error("🔥 Error disponibilidad:", e); 
        if (container) container.innerHTML = "<p style='color: #e94560;'>Error de conexión.</p>";
    }
}

function seleccionarHora(hora, elemento) {
    document.querySelectorAll('.slot').forEach(s => s.classList.remove('selected'));
    elemento.classList.add('selected');
    const inputHora = document.getElementById('citaHoraSeleccionada');
    if (inputHora) inputHora.value = hora;
}

async function guardarCita(e, token, user, rol) {
    e.preventDefault();
    
    const inputHora = document.getElementById('citaHoraSeleccionada');
    const hora = inputHora ? inputHora.value : null;
    const esCliente = rol.includes("CLIENTE");

    const selectCliente = document.getElementById('citaClienteId');
    const selectProv = document.getElementById('citaProveedorId');
    
    const clienteIdFinal = esCliente ? user.id : (selectCliente ? selectCliente.value : null);
    const proveedorIdFinal = esCliente ? (selectProv ? selectProv.value : null) : (user.proveedorId || user.id);

    if (!clienteIdFinal) return alert("Error: Selecciona un cliente.");
    if (!proveedorIdFinal) return alert("Error: Selecciona un profesional.");
    if (!hora) return alert("Debes seleccionar una hora.");

    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Procesando...';

    const dto = {
        clienteId: clienteIdFinal,
        proveedorId: proveedorIdFinal,
        servicioId: document.getElementById('citaServicioId').value,
        fecha: document.getElementById('citaFecha').value,
        hora: hora,
        modalidad: document.getElementById('citaModalidad').value,
        direccion: document.getElementById('citaDireccion').value.trim(),
        metodoRegistro: esCliente ? "Panel_Cliente" : "Barbero_Manual" 
    };

    try {
        const resp = await fetch(`http://localhost:5000/api/Citas/agendar`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json', 
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify(dto)
        });

        if (resp.ok) {
            alert("✅ Cita agendada exitosamente.");
            window.location.href = esCliente ? 'agendar-cita.html' : 'admin-dashboard.html';
        } else {
            const err = await resp.json();
            alert("❌ No se pudo agendar: " + (err.message || "Error desconocido"));
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