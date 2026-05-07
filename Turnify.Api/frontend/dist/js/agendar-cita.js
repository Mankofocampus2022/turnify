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
        // 🛡️ [NUEVO] Cargamos historial para clientes
        cargarMisCitas(user.id, token, "cliente");
    } else {
        if (sectionCliente) sectionCliente.style.display = 'block';
        if (sectionProveedor) sectionProveedor.style.display = 'none';
        if (subtitulo) subtitulo.innerText = "Registro manual de servicios (Local / Domicilio)";
        cargarClientes(token);
        const proveedorId = user.proveedorId || user.id;
        cargarServicios(proveedorId, token);
        // 🛡️ [NUEVO] Cargamos agenda para barberos
        cargarMisCitas(proveedorId, token, "proveedor");
    }

    // 🔥 [KILLER FIX] - VINCULACIÓN DE EVENTOS DE DISPONIBILIDAD
    const inputFecha = document.getElementById('citaFecha');
    const selectServicio = document.getElementById('citaServicioId');
    const selectModalidad = document.getElementById('citaModalidad');

    if (inputFecha) inputFecha.addEventListener('change', cargarDisponibilidad);
    if (selectServicio) selectServicio.addEventListener('change', cargarDisponibilidad);
    
    if (selectModalidad) {
        selectModalidad.addEventListener('change', () => {
            toggleDireccionCita();
            cargarDisponibilidad();
        });
    }

    const formCita = document.getElementById('formNuevaCita');
    if (formCita) {
        formCita.addEventListener('submit', (e) => guardarCita(e, token, user, rol));
    }
});

// --- [NUEVO] 📜 GESTIÓN DE MIS CITAS (VER, CANCELAR, CAMBIAR) ---
async function cargarMisCitas(id, token, tipo) {
    const container = document.getElementById('listaMisCitas'); 
    if (!container) return;

    console.log(`📡 [Fetch] Obteniendo agenda para ${tipo}...`);
    const url = tipo === "cliente" ? 
        `http://localhost:5000/api/Citas/historial/${id}` : 
        `http://localhost:5000/api/Citas/hoy`; 

    try {
        const resp = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (resp.ok) {
            const citas = await resp.json();
            renderizarCitas(citas, container, token);
        }
    } catch (e) { 
        console.error("🔥 Error cargando mis citas:", e);
        container.innerHTML = "<p style='color: #ff5e5e; font-size: 0.7rem;'>Error al cargar agenda.</p>";
    }
}

// 🛡️ REPARACIÓN DE RENDERIZADO: Mata el "asco" visual y aplica el estilo Glass
function renderizarCitas(citas, container, token) {
    if (!citas || citas.length === 0) {
        container.innerHTML = "<p style='color: rgba(255,255,255,0.5); font-size: 0.75rem; padding: 10px;'>No tienes citas registradas.</p>";
        return;
    }

    // 🚩 Inyectamos la estructura limpia que el CSS "Blindado" espera
    container.innerHTML = citas.map(c => `
        <div class="card-cita-sidebar ${c.estado.toLowerCase()}">
            ${c.estado === 'pendiente' ? `
                <button class="btn-cancelar-mini" onclick="cancelarCita('${c.id}', '${token}')" title="Cancelar Cita">
                    <i class="fas fa-times-circle"></i>
                </button>
            ` : ''}
            <h6>${c.servicioNombre || c.servicio || "Servicio"}</h6>
            <div class="cita-info">
                <i class="far fa-calendar-alt"></i> ${c.fecha.split('T')[0]}
            </div>
            <div class="cita-info">
                <i class="far fa-clock"></i> ${c.hora.slice(0, 5)}
            </div>
            <div class="cita-info">
                <i class="fas fa-info-circle"></i> ${c.estado.toUpperCase()}
            </div>
            ${c.tokenValidacion ? `
                <div class="token-tag">
                    <i class="fas fa-key"></i> TOKEN: ${c.tokenValidacion}
                </div>
            ` : ''}
        </div>
    `).join('');
}

// 🛡️ CANCELACIÓN BLINDADA
async function cancelarCita(id, token) {
    if (!confirm("⚠️ ¿Estás seguro de que deseas cancelar esta cita?")) return;

    try {
        const resp = await fetch(`http://localhost:5000/api/Citas/${id}/estado`, {
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
            const err = await resp.json();
            alert("❌ Error: " + (err.message || "No se pudo cancelar."));
        }
    } catch (e) { console.error("🔥 Error conexión cancelación:", e); }
}

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

    const container = document.getElementById('containerSlots');
    if (!fecha || !servicioId || !proveedorId) {
        if (container) container.innerHTML = "<p style='color: #48c1b5;'>Selecciona fecha y servicio.</p>";
        return;
    }

    if (container) container.innerHTML = "<p><i class='fas fa-spinner fa-spin'></i> Consultando...</p>";

    try {
        const resp = await fetch(`http://localhost:5000/api/Citas/disponibilidad?proveedorId=${proveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        if (resp.ok) {
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
        }
    } catch (e) { console.error("🔥 Error disponibilidad:", e); }
}

function seleccionarHora(hora, elemento) {
    document.querySelectorAll('.slot').forEach(s => s.classList.remove('selected'));
    elemento.classList.add('selected');
    const inputHora = document.getElementById('citaHoraSeleccionada');
    if (inputHora) inputHora.value = hora;
}

// 🛡️ REPARACIÓN DE GUARDAR CITA: Captura de Token y Observaciones
async function guardarCita(e, token, user, rol) {
    e.preventDefault();
    
    const inputHora = document.getElementById('citaHoraSeleccionada');
    const hora = inputHora ? inputHora.value : null;
    const esCliente = rol.includes("CLIENTE");

    const selectCliente = document.getElementById('citaClienteId');
    const selectProv = document.getElementById('citaProveedorId');
    
    const clienteIdFinal = esCliente ? user.id : (selectCliente ? selectCliente.value : null);
    const proveedorIdFinal = esCliente ? (selectProv ? selectProv.value : null) : (user.proveedorId || user.id);

    if (!clienteIdFinal || !proveedorIdFinal || !hora) return alert("Completa todos los campos y selecciona hora.");

    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Agendando...';

    // 🚩 CAPTURA DE OBSERVACIONES (Si el input existe en el HTML)
    const inputObs = document.getElementById('citaObservaciones');
    const observaciones = inputObs ? inputObs.value.trim() : "";

    const dto = {
        clienteId: clienteIdFinal,
        proveedorId: proveedorIdFinal,
        servicioId: document.getElementById('citaServicioId').value,
        fecha: document.getElementById('citaFecha').value,
        hora: hora,
        modalidad: document.getElementById('citaModalidad').value,
        direccion: document.getElementById('citaDireccion').value.trim(),
        observaciones: observaciones,
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

        const data = await resp.json();

        if (resp.ok) {
            // El mensaje del servidor ahora incluye el token V9MJ8U
            alert(`✅ ${data.message}`); 
            window.location.reload(); 
        } else {
            alert("❌ Error: " + (data.message || "No se pudo agendar."));
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