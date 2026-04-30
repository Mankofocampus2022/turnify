document.addEventListener('DOMContentLoaded', async () => {
    // 🛡️ Blindaje de sesión
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const userStr = localStorage.getItem('user');
    
    if (!token || !userStr) {
        window.location.href = 'login.html';
        return;
    }

    const user = JSON.parse(userStr);
    // 🚩 Limpiamos el rol de espacios o saltos de línea invisibles
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();
    const esCliente = rol.includes("CLIENTE");

    console.log("🚀 [Turnify Log] Rol Detectado:", rol);
    console.log("🚀 [Turnify Log] ¿Es Cliente?:", esCliente);

    // 🚩 LÓGICA DE INTERFAZ POR ROL
    const sectionCliente = document.getElementById('sectionSeleccionarCliente');
    const sectionProveedor = document.getElementById('sectionSeleccionarProveedor');
    const subtitulo = document.getElementById('subtituloAgendar');

    if (esCliente) {
        console.log("👤 Ejecutando Flujo Cliente...");
        if (sectionCliente) sectionCliente.style.display = 'none';
        if (sectionProveedor) sectionProveedor.style.display = 'block';
        if (subtitulo) subtitulo.innerText = "Reserva tu cita con tu profesional favorito.";
        
        cargarProveedores(token); 
    } else {
        console.log("💈 Ejecutando Flujo Barbero/Admin...");
        if (sectionCliente) sectionCliente.style.display = 'block';
        if (sectionProveedor) sectionProveedor.style.display = 'none';
        if (subtitulo) subtitulo.innerText = "Registro manual de servicios (Local / Domicilio)";
        
        // 🛡️ FIX: Solo intentamos cargar clientes si NO somos rol cliente
        cargarClientes(token);
        
        // Si es barbero, cargamos sus propios servicios de una vez
        const proveedorId = user.proveedorId || user.id;
        console.log("🎯 Barbero detectado. Cargando sus servicios con ID:", proveedorId);
        cargarServicios(proveedorId, token);
    }

    // Evento de guardado
    const formCita = document.getElementById('formNuevaCita');
    if (formCita) {
        formCita.addEventListener('submit', (e) => guardarCita(e, token, user, rol));
    }
});

/**
 * 🚩 NUEVO: Carga la lista de profesionales para que el cliente elija
 */
async function cargarProveedores(token) {
    console.log("📡 [Fetch] Cargando lista de Proveedores...");
    try {
        const resp = await fetch(`http://localhost:5000/api/Proveedores`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (resp.ok) {
            const proveedores = await resp.json();
            console.log("✅ Proveedores recibidos:", proveedores.length);
            const selectProv = document.getElementById('citaProveedorId');
            if (selectProv) {
                selectProv.innerHTML = '<option value="">-- Selecciona Profesional --</option>' + 
                    proveedores.map(p => `<option value="${p.id}">${p.nombreComercial || p.nombre}</option>`).join('');
                
                // 🚩 Forzamos la vinculación del evento
                selectProv.onchange = () => {
                    console.log("🖱️ [Event] Cambio detectado en Profesional");
                    cargarServiciosPorProveedor();
                };
            }
        } else {
            console.error("❌ Error al cargar proveedores. Status:", resp.status);
        }
    } catch (e) { console.error("🔥 Error proveedores:", e); }
}

/**
 * 🚩 NUEVO: Cuando el cliente elige profesional, cargamos sus servicios específicos
 */
async function cargarServiciosPorProveedor() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const selectProv = document.getElementById('citaProveedorId');
    if (!selectProv) {
        console.error("❌ No se encontró el elemento citaProveedorId");
        return;
    }
    
    const proveedorId = selectProv.value;
    console.log("🆔 [ID Seleccionado]:", proveedorId);

    if (proveedorId) {
        cargarServicios(proveedorId, token);
    } else {
        console.warn("⚠️ No se seleccionó ningún proveedorId válido.");
        const selectServicio = document.getElementById('citaServicioId');
        if (selectServicio) selectServicio.innerHTML = '<option value="">¿Qué servicio realizaremos?</option>';
    }
}

async function cargarServicios(proveedorId, token) {
    console.log("📡 [Fetch] Solicitando servicios al API para ID:", proveedorId);
    try {
        const resp = await fetch(`http://localhost:5000/api/Servicios/proveedor/${proveedorId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (resp.ok) {
            const servicios = await resp.json();
            console.log("✅ Servicios cargados con éxito:", servicios);

            const selectServicio = document.getElementById('citaServicioId');
            if (selectServicio) {
                if (servicios.length === 0) {
                    console.warn("Empty Data: El proveedor no tiene servicios registrados.");
                    selectServicio.innerHTML = '<option value="">Sin servicios disponibles</option>';
                } else {
                    selectServicio.innerHTML = '<option value="">Selecciona un servicio</option>' + 
                        servicios.map(s => `<option value="${s.id}">${s.nombre} ($${s.precio})</option>`).join('');
                }
            }
        } else {
            console.error("❌ Error API Servicios. Status:", resp.status);
            // Si el error es 500 o 404, el log nos dirá la verdad
            const errorText = await resp.text();
            console.log("📄 Respuesta del servidor:", errorText);
        }
    } catch (e) { console.error("🔥 Error servicios:", e); }
}

async function cargarClientes(token) {
    console.log("📡 [Fetch] Cargando lista de Clientes (Modo Admin)...");
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

async function cargarDisponibilidad() {
    const fecha = document.getElementById('citaFecha').value;
    const servicioId = document.getElementById('citaServicioId').value;
    const user = JSON.parse(localStorage.getItem('user'));
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    
    const selectProv = document.getElementById('citaProveedorId');
    const proveedorId = rol.includes("CLIENTE") ? 
        (selectProv ? selectProv.value : null) : 
        (user.proveedorId || user.id);

    console.log("📅 [Consultando Disponibilidad] Fecha:", fecha, "| Serv:", servicioId, "| Prov:", proveedorId);

    const container = document.getElementById('containerSlots');
    if (!fecha || !servicioId || !proveedorId) return;

    if (container) container.innerHTML = "<p style='color: #48c1b5;'>Consultando agenda...</p>";

    try {
        const resp = await fetch(`http://localhost:5000/api/Citas/disponibilidad?proveedorId=${proveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        if (resp.ok) {
            const slots = await resp.json();
            if (container) {
                if(slots.length === 0) {
                    container.innerHTML = "<p style='color: #e94560;'>Sin disponibilidad para este día.</p>";
                    return;
                }
                container.innerHTML = slots.map(h => 
                    `<div class="slot" onclick="seleccionarHora('${h}', this)">${h.slice(0, 5)}</div>`
                ).join('');
            }
        }
    } catch (e) { console.error("🔥 Error disponibilidad:", e); }
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

    console.log("📦 [Enviando Cita DTO]:", dto);

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