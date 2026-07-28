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

// Variable global para almacenar temporalmente la foto seleccionada
let archivoFotoEmpleado = null;

// Variable de control para saber si este usuario maneja personal/sillas o no
let tienePersonalOEquipo = true;

document.addEventListener('DOMContentLoaded', async () => {
    // Validar autenticación preliminar
    if (!token) {
        localStorage.clear();
        window.location.href = 'login.html';
        return;
    }

    // 🛡️ CONTROL DE ACCESO POR ROLES Y DATOS (RBAC DINÁMICO)
    const rolRaw = (localStorage.getItem('usuario_rol') || localStorage.getItem('user_role') || '').toUpperCase();
    
    // Si el rol es explícitamente Staff/Barbero/Manicurista
    const esStaffColaborador = rolRaw.includes('STAFF') || 
                               rolRaw.includes('BARBERO') || 
                               rolRaw.includes('MANICURISTA') || 
                               rolRaw.includes('EMPLEADO');

    if (esStaffColaborador) {
        tienePersonalOEquipo = false;
        ocultarPestañasRestringidas();
        const sub = document.getElementById('subtituloDirectorio');
        if (sub) sub.innerText = 'Directorio de Clientes asignados a tu negocio.';

        inicializarFormularios();
        switchTab('clientes');
    } else {
        inicializarFormularios();
        // 🚀 VERIFICACIÓN EN API: Si es un Proveedor Independiente (sin empleados), oculta las pestañas
        await verificarEstructuraNegocio();
    }

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

    // Listener para el input file de la foto del empleado
    const inputFoto = document.getElementById('staffFoto');
    if (inputFoto) {
        inputFoto.addEventListener('change', (e) => {
            const file = e.target.files[0];
            const msgError = document.getElementById('msgErrorFoto');
            if (msgError) {
                msgError.style.display = 'none';
                msgError.innerText = '';
            }

            if (!file) {
                archivoFotoEmpleado = null;
                return;
            }

            const validTypes = ['image/jpeg', 'image/png', 'image/webp'];
            if (!validTypes.includes(file.type)) {
                if (msgError) {
                    msgError.innerText = 'Solo se permiten imágenes JPG, PNG o WEBP.';
                    msgError.style.display = 'block';
                } else {
                    alert('Solo se permiten imágenes JPG, PNG o WEBP.');
                }
                inputFoto.value = '';
                archivoFotoEmpleado = null;
                return;
            }

            const maxSize = 2 * 1024 * 1024;
            if (file.size > maxSize) {
                if (msgError) {
                    msgError.innerText = 'La imagen supera el límite máximo de 2MB.';
                    msgError.style.display = 'block';
                } else {
                    alert('La imagen supera el límite máximo de 2MB.');
                }
                inputFoto.value = '';
                archivoFotoEmpleado = null;
                return;
            }

            archivoFotoEmpleado = file;
            const previewImg = document.getElementById('previewFotoEmpleado');
            const previewFallback = document.getElementById('previewInitialsFallback');

            if (previewImg) {
                previewImg.src = URL.createObjectURL(file);
                previewImg.style.display = 'block';
            }
            if (previewFallback) {
                previewFallback.style.display = 'none';
            }
        });
    }
});

// 🛡️ OCULTAR PESTAÑAS: Remueve físicamente del navegador las opciones de Personal y Sillas
function ocultarPestañasRestringidas() {
    const btnStaff = document.getElementById('tab-btn-staff') || document.querySelector("button[onclick*='staff']");
    const btnEstaciones = document.getElementById('tab-btn-estaciones') || document.querySelector("button[onclick*='estaciones']");
    const btnUsuarios = document.getElementById('tab-btn-usuarios') || document.querySelector("button[onclick*='usuarios']");

    if (btnStaff) btnStaff.style.setProperty('display', 'none', 'important');
    if (btnEstaciones) btnEstaciones.style.setProperty('display', 'none', 'important');
    if (btnUsuarios) btnUsuarios.style.setProperty('display', 'none', 'important');

    const tabStaffContent = document.getElementById('tab-staff');
    const tabEstacionesContent = document.getElementById('tab-estaciones');
    const tabUsuariosContent = document.getElementById('tab-usuarios');

    if (tabStaffContent) tabStaffContent.style.setProperty('display', 'none', 'important');
    if (tabEstacionesContent) tabEstacionesContent.style.setProperty('display', 'none', 'important');
    if (tabUsuariosContent) tabUsuariosContent.style.setProperty('display', 'none', 'important');
}

// 🧠 VERIFICA SI EL PROVEEDOR TIENE EQUIPO O ES INDEPENDIENTE
async function verificarEstructuraNegocio() {
    try {
        const response = await fetch(`${API_BASE}/Empleados`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const empleados = await response.json();
            if (empleados && empleados.length > 0) {
                // Si tiene empleados registrados, habilita las pestañas normalmente
                tienePersonalOEquipo = true;
                switchTab('staff');
                return;
            }
        }
    } catch (err) {
        console.error("Error al consultar estructura del negocio:", err);
    }

    // Si NO tiene empleados (Proveedor independiente / Manicurista sin equipo), OCULTA LAS PESTAÑAS Y VA DIRECTO A CLIENTES
    tienePersonalOEquipo = false;
    ocultarPestañasRestringidas();
    switchTab('clientes');
}

/* ============================================================================
   🧠 CONTROL DE PESTAÑAS (TABS)
   ============================================================================ */
window.switchTab = function(tabName) {
    // Si el usuario no tiene personal o es independiente, se le bloquea el acceso a otras pestañas
    if (!tienePersonalOEquipo && (tabName === 'staff' || tabName === 'estaciones' || tabName === 'usuarios')) {
        tabName = 'clientes';
    }

    currentTab = tabName;
    
    // Cambiar clases activas en los botones
    const botones = document.querySelectorAll('.tab-btn');
    botones.forEach(btn => btn.classList.remove('active'));
    
    // Encontrar el botón clickeado por su atributo onclick
    const botonActivo = Array.from(botones).find(btn => btn.getAttribute('onclick')?.includes(`'${tabName}'`));
    if (botonActivo) botonActivo.classList.add('active');

    // Cambiar visibilidad de los contenedores de contenido
    const contenidos = document.querySelectorAll('.tab-content');
    contenidos.forEach(cont => cont.classList.remove('active'));

    const contenidoActivo = document.getElementById(`tab-${tabName}`);
    if (contenidoActivo) contenidoActivo.classList.add('active');

    // Mantiene ocultas las pestañas prohibidas
    if (!tienePersonalOEquipo) {
        ocultarPestañasRestringidas();
    }

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
function obtenerAvatarHtml(nombre, fotoUrl) {
    const partes = (nombre || '').trim().split(' ');
    let iniciales = '??';
    if (partes.length >= 2) {
        iniciales = `${partes[0].charAt(0)}${partes[1].charAt(0)}`.toUpperCase();
    } else if (partes.length === 1 && partes[0].length > 0) {
        iniciales = partes[0].substring(0, 2).toUpperCase();
    }

    const htmlFallback = `<div style="width:40px; height:40px; border-radius:50%; background:#4e54c8; color:white; font-weight:bold; display:flex; align-items:center; justify-content:center; font-size:14px; flex-shrink:0; text-transform:uppercase;">${iniciales}</div>`;

    if (!fotoUrl) {
        return htmlFallback;
    }

    const fullFotoUrl = fotoUrl.startsWith('http') ? fotoUrl : `${API_HOST}${fotoUrl.startsWith('/') ? '' : '/'}${fotoUrl}`;

    return `
        <img 
            src="${fullFotoUrl}" 
            alt="${nombre}" 
            loading="lazy" 
            style="width:40px; height:40px; border-radius:50%; object-fit:cover; border:1px solid #ccc; flex-shrink:0;" 
            onerror="this.onerror=null; this.outerHTML=\`${htmlFallback}\`;"
        />
    `;
}

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

        tbody.innerHTML = empleados.map(emp => {
            const accionesHtml = !tienePersonalOEquipo 
                ? `<span style="opacity:0.5; font-size:0.85rem;"><i class="fas fa-lock"></i> Solo Lectura</span>`
                : `
                    <button class="btn-filter" onclick="editarEmpleado('${emp.id}')" style="background:#48c1b5; color:white; border:none; padding:5px 10px; border-radius:5px; cursor:pointer; margin-right:5px;" title="Editar">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn-filter" onclick="eliminarEmpleado('${emp.id}')" style="background:#e94560; color:white; border:none; padding:5px 10px; border-radius:5px; cursor:pointer;" title="Eliminar">
                        <i class="fas fa-trash"></i>
                    </button>
                `;

            return `
                <tr>
                    <td>
                        <div style="display:flex; align-items:center; gap:12px;">
                            ${obtenerAvatarHtml(emp.nombre, emp.fotoUrl || emp.foto)}
                            <div>
                                <strong>${emp.nombre}</strong><br>
                                <small style="opacity:0.6;">${emp.email || 'Sin email de acceso'}</small>
                            </div>
                        </div>
                    </td>
                    <td>${emp.telefono || 'N/A'}</td>
                    <td><span class="status-pill status-pendiente">${emp.tipoContrato}</span></td>
                    <td>${emp.tipoContrato === 'Porcentaje' ? `${emp.valorContrato}%` : `$${emp.valorContrato.toLocaleString()}`}</td>
                    <td><span class="status-pill ${emp.activo ? 'status-activo' : 'status-bloqueado'}">${emp.activo ? 'Activo' : 'Inactivo'}</span></td>
                    <td>${accionesHtml}</td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        console.error(err);
        tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:#e94560;">Error al conectar con el servidor.</td></tr>';
    }
}

window.abrirModalStaff = function() {
    if (!tienePersonalOEquipo) {
        alert("⚠️ No tienes permisos para registrar nuevo personal.");
        return;
    }

    const form = document.getElementById('formStaff');
    if (form) form.reset();
    
    document.getElementById('staffId').value = '';
    archivoFotoEmpleado = null;

    const previewImg = document.getElementById('previewFotoEmpleado');
    const previewFallback = document.getElementById('previewInitialsFallback');
    const msgError = document.getElementById('msgErrorFoto');

    if (previewImg) {
        previewImg.src = '';
        previewImg.style.display = 'none';
    }
    if (previewFallback) previewFallback.style.display = 'flex';
    if (msgError) msgError.style.display = 'none';

    document.getElementById('modalStaffTitulo').innerText = 'Registrar Empleado';
    document.getElementById('modalStaff').style.display = 'flex';
}

window.cerrarModalStaff = function() {
    document.getElementById('modalStaff').style.display = 'none';
    archivoFotoEmpleado = null;
}

window.editarEmpleado = async function(id) {
    if (!tienePersonalOEquipo) {
        alert("⚠️ No tienes permisos para modificar información del personal.");
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/Empleados/${id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!response.ok) throw new Error('No se pudo obtener el empleado');

        const emp = await response.json();

        document.getElementById('staffId').value = emp.id;
        document.getElementById('staffNombre').value = emp.nombre || '';
        document.getElementById('staffTelefono').value = emp.telefono || '';
        document.getElementById('staffTipoContrato').value = emp.tipoContrato || 'Fijo';
        document.getElementById('staffValorContrato').value = emp.valorContrato || 0;
        document.getElementById('staffEmail').value = emp.email || '';

        document.getElementById('staffTipoContrato').dispatchEvent(new Event('change'));

        const previewImg = document.getElementById('previewFotoEmpleado');
        const previewFallback = document.getElementById('previewInitialsFallback');

        const fotoUrl = emp.fotoUrl || emp.foto;
        if (fotoUrl && previewImg) {
            const fullFotoUrl = fotoUrl.startsWith('http') ? fotoUrl : `${API_HOST}${fotoUrl.startsWith('/') ? '' : '/'}${fotoUrl}`;
            previewImg.src = fullFotoUrl;
            previewImg.style.display = 'block';
            if (previewFallback) previewFallback.style.display = 'none';
        } else {
            if (previewImg) previewImg.style.display = 'none';
            if (previewFallback) previewFallback.style.display = 'flex';
        }

        document.getElementById('modalStaffTitulo').innerText = 'Editar Empleado';
        document.getElementById('modalStaff').style.display = 'flex';
    } catch (err) {
        console.error(err);
        alert('⚠️ No se pudo obtener la información del empleado.');
    }
}

async function subirFotoEmpleado(idEmpleado) {
    if (!archivoFotoEmpleado) return true;

    const formData = new FormData();
    formData.append('foto', archivoFotoEmpleado);

    try {
        const response = await fetch(`${API_BASE}/Empleados/${idEmpleado}/foto`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${token}`
            },
            body: formData
        });

        if (!response.ok) {
            console.error('Error al subir la imagen del empleado');
            return false;
        }
        return true;
    } catch (err) {
        console.error('Error de red al subir foto:', err);
        return false;
    }
}

async function guardarEmpleado(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarStaff');
    const origText = btn.innerText;
    btn.disabled = true;
    btn.innerText = 'Procesando...';

    const idExistente = document.getElementById('staffId').value;

    const payload = {
        Nombre: document.getElementById('staffNombre').value.trim(),
        Telefono: document.getElementById('staffTelefono').value.trim(),
        TipoContrato: document.getElementById('staffTipoContrato').value,
        ValorContrato: parseFloat(document.getElementById('staffValorContrato').value),
        Email: document.getElementById('staffEmail').value.trim() || null,
        Password: document.getElementById('staffPassword').value || null
    };

    try {
        const esEdicion = !!idExistente;
        const endpoint = esEdicion ? `${API_BASE}/Empleados/${idExistente}` : `${API_BASE}/Empleados`;
        const metodo = esEdicion ? 'PUT' : 'POST';

        const response = await fetch(endpoint, {
            method: metodo,
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const empleadoGuardado = await response.json();
            const idEmpleado = idExistente || empleadoGuardado.id || empleadoGuardado.Id;

            if (archivoFotoEmpleado && idEmpleado) {
                btn.innerText = 'Subiendo imagen...';
                await subirFotoEmpleado(idEmpleado);
            }

            alert(esEdicion ? '🎉 Empleado actualizado con éxito.' : '🎉 Empleado y puesto de trabajo configurados con éxito.');
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
    if (!tienePersonalOEquipo) {
        alert("⚠️ No tienes permisos para remover colaboradores.");
        return;
    }

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

        tbody.innerHTML = estaciones.map(est => {
            const accionesHtml = !tienePersonalOEquipo 
                ? `<span style="opacity:0.5; font-size:0.85rem;"><i class="fas fa-lock"></i> Solo Lectura</span>`
                : `
                    <button class="btn-filter" onclick="eliminarEstacion('${est.id}')" style="background:#e94560; color:white; border:none; padding:5px 10px; border-radius:5px; cursor:pointer;">
                        <i class="fas fa-trash"></i>
                    </button>
                `;

            return `
                <tr>
                    <td><strong><i class="fas fa-chair" style="color:#48c1b5;"></i> ${est.nombreSilla}</strong></td>
                    <td>${est.descripcion || 'Sin descripción'}</td>
                    <td><span class="status-pill ${est.activa ? 'status-activo' : 'status-bloqueado'}">${est.activa ? 'Disponible' : 'Mantenimiento'}</span></td>
                    <td>${accionesHtml}</td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:#e94560;">Error de comunicación.</td></tr>';
    }
}

window.abrirModalEstacion = function() {
    if (!tienePersonalOEquipo) {
        alert("⚠️ No tienes permisos para registrar nuevas estaciones de trabajo.");
        return;
    }

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
    if (!tienePersonalOEquipo) {
        alert("⚠️ No tienes permisos para eliminar estaciones de trabajo.");
        return;
    }

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
   Wait FLUJO 3: MIS CLIENTES (WEB REGISTRADOS)
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
   💻 FLUJO 4: USUARIOS DEL SISTEMA
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