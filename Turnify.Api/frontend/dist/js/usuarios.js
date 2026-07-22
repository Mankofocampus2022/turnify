/* ============================================================================
   TURNIFY - MOTOR DE GESTIÓN DE DIRECTORIO, PERSONAL Y ESTACIONES (HU 001)
   ============================================================================ */

const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : `${window.location.protocol}//${window.location.hostname}:5000`;

const API_BASE = `${API_HOST}/api`;

// Estado global de la vista
let currentTab = 'staff';
const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

document.addEventListener('DOMContentLoaded', () => {
    // Validar autenticación preliminar
    if (!token) {
        localStorage.clear();
        window.location.href = 'login.html';
        return;
    }

    // Inicializar listeners de los formularios
    inicializarFormularios();

    // Carga por defecto de la primera pestaña
    cargarDatosPestaña('staff');

    // Manejo dinámico de etiquetas en formulario de empleados según tipo de contrato
    const selectContrato = document.getElementById('staffTipoContrato');
    if (selectContrato) {
        selectContrato.addEventListener('change', (e) => {
            const lbl = document.getElementById('lblValorContrato');
            const input = document.getElementById('staffValorContrato');
            if (e.target.value === 'Fijo') {
                if (lbl) lbl.innerText = 'Salario Fijo Mensual ($)';
                if (input) input.placeholder = 'Ej: 1500000';
            } else {
                if (lbl) lbl.innerText = 'Porcentaje de Comisión (%)';
                if (input) input.placeholder = 'Ej: 50';
            }
        });
    }
});

/* ============================================================================
   🧠 CONTROL DE PESTAÑAS (TABS)
   ============================================================================ */
window.switchTab = function(tabName) {
    currentTab = tabName;
    
    // Cambiar clases activas en los botones
    const botones = document.querySelectorAll('.tab-btn');
    botones.forEach(btn => btn.classList.remove('active'));
    
    // Encontrar el botón clickeado por su atributo onclick
    const botonActivo = Array.from(botones).find(btn => btn.getAttribute('onclick').includes(`'${tabName}'`));
    if (botonActivo) botonActivo.classList.add('active');

    // Cambiar visibilidad de los contenedores de contenido
    const contenidos = document.querySelectorAll('.tab-content');
    contenidos.forEach(cont => cont.classList.remove('active'));

    const contenidoActivo = document.getElementById(`tab-${tabName}`);
    if (contenidoActivo) contenidoActivo.classList.add('active');

    // Cargar los datos específicos de la pestaña seleccionada
    cargarDatosPestaña(tabName);
}

function cargarDatosPestaña(tab) {
    switch (tab) {
        case 'staff':
            listarPersonal();
            break;
        case 'estaciones':
            listarEstaciones();
            break;
        case 'clientes':
            listarClientes();
            break;
        case 'usuarios':
            listarUsuariosSistema();
            break;
    }
}

/* ============================================================================
   👥 FLUJO 1: MI PERSONAL (STAFF / EMPLEADOS)
   ============================================================================ */
async function listarPersonal() {
    const tbody = document.getElementById('tablaStaff');
    if (!tbody) return;

    try {
        const response = await fetch(`${API_BASE}/Empleados`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!response.ok) throw new Error('Error al leer el personal');
        
        const empleados = await response.json();
        if (empleados.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;">No hay empleados registrados.</td></tr>';
            return;
        }

        tbody.innerHTML = empleados.map(emp => `
            <tr>
                <td><strong>${emp.nombre}</strong><br><small style="opacity:0.6;">${emp.email || 'Sin email de acceso'}</small></td>
                <td>${emp.telefono || 'N/A'}</td>
                <td><span class="status-pill status-pendiente">${emp.tipoContrato}</span></td>
                <td>${emp.tipoContrato === 'Porcentaje' ? `${emp.valorContrato}%` : `$${emp.valorContrato.toLocaleString()}`}</td>
                <td><span class="status-pill ${emp.activo ? 'status-activo' : 'status-bloqueado'}">${emp.activo ? 'Activo' : 'Inactivo'}</span></td>
                <td>
                    <button class="btn-filter" onclick="eliminarEmpleado('${emp.id}')" style="background:#e94560; color:white; border:none; padding:5px 10px; border-radius:5px; cursor:pointer;">
                        <i class="fas fa-trash"></i>
                    </button>
                </td>
            </tr>
        `).join('');
    } catch (err) {
        console.error(err);
        tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:#e94560;">Error al conectar con el servidor.</td></tr>';
    }
}

window.abrirModalStaff = function() {
    document.getElementById('formStaff').reset();
    document.getElementById('staffId').value = '';
    document.getElementById('modalStaffTitulo').innerText = 'Registrar Empleado';
    document.getElementById('modalStaff').style.display = 'flex';
}

window.cerrarModalStaff = function() {
    document.getElementById('modalStaff').style.display = 'none';
}

async function guardarEmpleado(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarStaff');
    const origText = btn.innerText;
    btn.disabled = true;
    btn.innerText = 'Procesando...';

    const payload = {
        Nombre: document.getElementById('staffNombre').value.trim(),
        Telefono: document.getElementById('staffTelefono').value.trim(),
        TipoContrato: document.getElementById('staffTipoContrato').value,
        ValorContrato: parseFloat(document.getElementById('staffValorContrato').value),
        Email: document.getElementById('staffEmail').value.trim() || null,
        Password: document.getElementById('staffPassword').value || null
    };

    try {
        const response = await fetch(`${API_BASE}/Empleados`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            alert('🎉 Empleado y puesto de trabajo configurados con éxito.');
            cerrarModalStaff();
            listarPersonal();
        } else {
            const errData = await response.json();
            alert(`⚠️ Error: ${errData.message || 'No se pudo guardar el empleado.'}`);
        }
    } catch (err) {
        alert('❌ Error crítico de red al guardar el personal.');
    } finally {
        btn.disabled = false;
        btn.innerText = origText;
    }
}

window.eliminarEmpleado = async function(id) {
    if (!confirm('¿Seguro que deseas remover este empleado de la plantilla?')) return;
    try {
        const response = await fetch(`${API_BASE}/Empleados/${id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            listarPersonal();
        }
    } catch (err) { console.error(err); }
}

/* ============================================================================
   🪑 FLUJO 2: SILLAS / ESTACIONES DE TRABAJO
   ============================================================================ */
async function listarEstaciones() {
    const tbody = document.getElementById('tablaEstaciones');
    if (!tbody) return;

    try {
        const response = await fetch(`${API_BASE}/EstacionesTrabajo`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!response.ok) throw new Error('Error al leer estaciones');
        
        const estaciones = await response.json();
        if (estaciones.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">No hay estaciones configuradas.</td></tr>';
            return;
        }

        tbody.innerHTML = estaciones.map(est => `
            <tr>
                <td><strong><i class="fas fa-chair" style="color:#48c1b5;"></i> ${est.nombreSilla}</strong></td>
                <td>${est.descripcion || 'Sin descripción'}</td>
                <td><span class="status-pill ${est.activa ? 'status-activo' : 'status-bloqueado'}">${est.activa ? 'Disponible' : 'Mantenimiento'}</span></td>
                <td>
                    <button class="btn-filter" onclick="eliminarEstacion('${est.id}')" style="background:#e94560; color:white; border:none; padding:5px 10px; border-radius:5px; cursor:pointer;">
                        <i class="fas fa-trash"></i>
                    </button>
                </td>
            </tr>
        `).join('');
    } catch (err) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:#e94560;">Error de comunicación.</td></tr>';
    }
}

window.abrirModalEstacion = function() {
    document.getElementById('formEstacion').reset();
    document.getElementById('estacionId').value = '';
    document.getElementById('modalEstacionTitulo').innerText = 'Registrar Estación / Silla';
    document.getElementById('modalEstacion').style.display = 'flex';
}

window.cerrarModalEstacion = function() {
    document.getElementById('modalEstacion').style.display = 'none';
}

async function guardarEstacion(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarEstacion');
    btn.disabled = true;

    const payload = {
        NombreSilla: document.getElementById('estacionNombre').value.trim(),
        Descripcion: document.getElementById('estacionDescripcion').value.trim()
    };

    try {
        const response = await fetch(`${API_BASE}/EstacionesTrabajo`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            cerrarModalEstacion();
            listarEstaciones();
        } else {
            alert('Error al mapear la estación.');
        }
    } catch (err) {
        console.error(err);
    } finally {
        btn.disabled = false;
    }
}

window.eliminarEstacion = async function(id) {
    if (!confirm('¿Deseas desvincular esta estación de trabajo?')) return;
    try {
        const response = await fetch(`${API_BASE}/EstacionesTrabajo/${id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) listarEstaciones();
    } catch (err) { console.error(err); }
}

/* ============================================================================
   🙍‍♂️ FLUJO 3: MIS CLIENTES (WEB REGISTRADOS)
   ============================================================================ */
async function listarClientes() {
    const tbody = document.getElementById('tablaClientes');
    if (!tbody) return;

    try {
        const response = await fetch(`${API_BASE}/Clientes`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!response.ok) throw new Error();
        
        const clientes = await response.json();
        if (clientes.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">No hay clientes registrados desde la web aún.</td></tr>';
            return;
        }

        tbody.innerHTML = clientes.map(cli => {
            const fecha = cli.fechaRegistro ? new Date(cli.fechaRegistro).toLocaleDateString('es-CO') : 'N/A';
            return `
                <tr>
                    <td><strong>${cli.nombre}</strong></td>
                    <td><i class="fab fa-whatsapp" style="color:#25d366;"></i> ${cli.telefono || 'N/A'}</td>
                    <td>${cli.email || 'N/A'}</td>
                    <td><small>${fecha}</small></td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">No se pudo procesar el listado de clientes.</td></tr>';
    }
}

/* ============================================================================
   💻 FLUJO 4: USUARIOS DEL SISTEMA (Mantenimiento de tu Lógica Original)
   ============================================================================ */
async function listarUsuariosSistema() {
    const tbody = document.getElementById('tablaUsuarios');
    if (!tbody) return;

    try {
        const response = await fetch(`${API_BASE}/Usuarios`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!response.ok) throw new Error();
        
        const usuarios = await response.json();
        tbody.innerHTML = usuarios.map(usr => {
            const badgeBloqueo = usr.estaBloqueado 
                ? `<span class="status-pill status-bloqueado">Bloqueado</span>` 
                : `<span class="status-pill status-activo">Activo</span>`;
                
            return `
                <tr>
                    <td><strong>${usr.nombre}</strong></td>
                    <td>${usr.email}</td>
                    <td><span class="status-pill status-pendiente" style="text-transform:uppercase;">${usr.rolNombre || 'Usuario'}</span></td>
                    <td><small>${usr.vencimientoSuscripcion ? new Date(usr.vencimientoSuscripcion).toLocaleDateString() : 'N/A'}</small></td>
                    <td>${badgeBloqueo}</td>
                    <td>
                        <button onclick="conmutarBloqueoUsuario('${usr.id}', ${usr.estaBloqueado})" class="btn-add" style="background:${usr.estaBloqueado ? '#48c1b5' : '#e94560'}; padding: 6px 12px; font-size:0.8rem;">
                            ${usr.estaBloqueado ? '<i class="fas fa-unlock"></i> Desbloquear' : '<i class="fas fa-lock"></i> Bloquear'}
                        </button>
                    </td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        console.error(err);
    }
}

window.conmutarBloqueoUsuario = async function(id, estadoActual) {
    const accion = estadoActual ? 'desbloquear' : 'bloquear';
    if (!confirm(`¿Seguro que deseas ${accion} este usuario en el sistema?`)) return;

    try {
        const endpoint = estadoActual ? `${API_BASE}/Usuarios/${id}/desbloquear` : `${API_BASE}/Usuarios/${id}/bloquear`;
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            listarUsuariosSistema();
        }
    } catch (err) { console.error(err); }
}

/* ============================================================================
   UTILIDADES COMPLEMENTARIAS
   ============================================================================ */
function inicializarFormularios() {
    const fStaff = document.getElementById('formStaff');
    if (fStaff) fStaff.addEventListener('submit', guardarEmpleado);

    const fEstacion = document.getElementById('formEstacion');
    if (fEstacion) fEstacion.addEventListener('submit', guardarEstacion);
}

window.logout = function() {
    if (confirm("¿Deseas cerrar sesión en Turnify?")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
} 