/* ============================================================================
   TURNIFY - MOTOR DE GESTIÓN DE DIRECTORIO, PERSONAL Y ESTACIONES (HU 001, HU-DIR01 & HU-DIR02)
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
let esUsuarioAdminSaaS = false;

document.addEventListener('DOMContentLoaded', async () => {
    // Validar autenticación preliminar
    if (!token) {
        localStorage.clear();
        window.location.href = 'login.html';
        return;
    }

    // 🛡️ CONTROL DE ACCESO POR ROLES Y BANDERAS (HU-DIR01 RBAC)
    const userStr = localStorage.getItem('user');
    const userObj = userStr ? JSON.parse(userStr) : null;
    
    let rolDetectado = (userObj?.rol || userObj?.rolNombre || localStorage.getItem('usuario_rol') || "").toLowerCase();

    // Intentar decodificar el token JWT si no viene en el localStorage
    if (!rolDetectado && token) {
        try {
            const base64Url = token.split('.')[1];
            if (base64Url) {
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
                const tokenData = JSON.parse(jsonPayload);
                rolDetectado = String(tokenData.role || tokenData["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || "").toLowerCase();
            }
        } catch (e) {
            console.warn("⚠️ No se pudo extraer la claim de rol del JWT:", e);
        }
    }

    esUsuarioAdminSaaS = rolDetectado.includes("admin") || rolDetectado.includes("superadmin");

    const esIndependienteRaw = localStorage.getItem('es_independiente') || localStorage.getItem('turnify_es_independiente');
    const esIndependiente = esIndependienteRaw === 'true';

    // 🚀 APLICACIÓN DE LA REGLA DE NEGOCIO HU-DIR01:
    if (esUsuarioAdminSaaS) {
        // SuperAdmin / Admin solo ven la pestaña "Usuarios (Sistema)"
        tienePersonalOEquipo = false;
        configurarVistasSegunRol('admin');
        const sub = document.getElementById('subtituloDirectorio');
        if (sub) sub.innerText = 'Administración global de cuentas y suscripciones de la plataforma.';

        inicializarFormularios();
        switchTab('usuarios');
    } else if (esIndependiente) {
        // Proveedor Independiente solo ve "Mis Clientes"
        tienePersonalOEquipo = false;
        configurarVistasSegunRol('independiente');
        const sub = document.getElementById('subtituloDirectorio');
        if (sub) sub.innerText = 'Directorio de Clientes asignados a tu negocio.';

        inicializarFormularios();
        switchTab('clientes');
    } else {
        // Staff / Administrador de Sede opera sobre Personal, Sillas y Clientes
        tienePersonalOEquipo = true;
        configurarVistasSegunRol('staff');
        inicializarFormularios();
        switchTab('staff');
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

// 🛡️ HU-DIR01: CONFIGURA VISIBILIDAD DE PESTAÑAS SEGÚN EL ROL
function configurarVistasSegunRol(perfil) {
    const btnStaff = document.getElementById('tab-btn-staff');
    const btnEstaciones = document.getElementById('tab-btn-estaciones');
    const btnClientes = document.getElementById('tab-btn-clientes');
    const btnUsuarios = document.getElementById('tab-btn-usuarios');

    if (perfil === 'admin') {
        if (btnStaff) btnStaff.style.setProperty('display', 'none', 'important');
        if (btnEstaciones) btnEstaciones.style.setProperty('display', 'none', 'important');
        if (btnClientes) btnClientes.style.setProperty('display', 'none', 'important');
        if (btnUsuarios) btnUsuarios.style.setProperty('display', 'inline-block', 'important');
    } else if (perfil === 'independiente') {
        if (btnStaff) btnStaff.style.setProperty('display', 'none', 'important');
        if (btnEstaciones) btnEstaciones.style.setProperty('display', 'none', 'important');
        if (btnUsuarios) btnUsuarios.style.setProperty('display', 'none', 'important');
        if (btnClientes) btnClientes.style.setProperty('display', 'inline-block', 'important');
    } else { // Staff / Sede
        if (btnStaff) btnStaff.style.setProperty('display', 'inline-block', 'important');
        if (btnEstaciones) btnEstaciones.style.setProperty('display', 'inline-block', 'important');
        if (btnClientes) btnClientes.style.setProperty('display', 'inline-block', 'important');
        if (btnUsuarios) btnUsuarios.style.setProperty('display', 'none', 'important');
    }
}

function ocultarPestañasRestringidas() {
    configurarVistasSegunRol(esUsuarioAdminSaaS ? 'admin' : 'independiente');
}

/* ============================================================================
   🧠 CONTROL DE PESTAÑAS (TABS)
   ============================================================================ */
window.switchTab = function(tabName) {
    if (esUsuarioAdminSaaS && tabName !== 'usuarios') {
        tabName = 'usuarios';
    } else if (!tienePersonalOEquipo && !esUsuarioAdminSaaS && (tabName === 'staff' || tabName === 'estaciones' || tabName === 'usuarios')) {
        tabName = 'clientes';
    }

    currentTab = tabName;
    
    const botones = document.querySelectorAll('.tab-btn');
    botones.forEach(btn => btn.classList.remove('active'));
    
    const botonActivo = Array.from(botones).find(btn => btn.getAttribute('onclick')?.includes(`'${tabName}'`));
    if (botonActivo) botonActivo.classList.add('active');

    const contenidos = document.querySelectorAll('.tab-content');
    contenidos.forEach(cont => cont.classList.remove('active'));

    const contenidoActivo = document.getElementById(`tab-${tabName}`);
    if (contenidoActivo) contenidoActivo.classList.add('active');

    cargarDatosPestaña(tabName);
};

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

            const emailMostrar = emp.emailUsuarioVinculado || emp.email || 'Sin email de acceso';

            return `
                <tr>
                    <td>
                        <div style="display:flex; align-items:center; gap:12px;">
                            ${obtenerAvatarHtml(emp.nombre, emp.fotoUrl || emp.foto)}
                            <div>
                                <strong>${emp.nombre}</strong><br>
                                <small style="opacity:0.6;">${emailMostrar}</small>
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
};

window.cerrarModalStaff = function() {
    document.getElementById('modalStaff').style.display = 'none';
    archivoFotoEmpleado = null;
};

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
        document.getElementById('staffEmail').value = emp.emailUsuarioVinculado || emp.email || '';

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
};

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
    const origText = btn ? btn.innerText : 'Guardar Empleado';
    if (btn) {
        btn.disabled = true;
        btn.innerText = 'Procesando...';
    }

    const idExistente = document.getElementById('staffId') ? document.getElementById('staffId').value : '';
    const tipoContratoRaw = document.getElementById('staffTipoContrato') ? document.getElementById('staffTipoContrato').value : 'Fijo';
    const valorContratoNum = parseFloat(document.getElementById('staffValorContrato') ? document.getElementById('staffValorContrato').value : 0) || 0;

    let tipoContratoFinal = 'Porcentaje';
    if (tipoContratoRaw.toLowerCase().includes('fijo')) {
        tipoContratoFinal = 'Fijo';
    }

    const nombreVal = document.getElementById('staffNombre') ? document.getElementById('staffNombre').value.trim() : '';
    const telefonoVal = document.getElementById('staffTelefono') ? document.getElementById('staffTelefono').value.trim() : '';
    const emailVal = document.getElementById('staffEmail') ? document.getElementById('staffEmail').value.trim() : '';
    const passVal = document.getElementById('staffPassword') ? document.getElementById('staffPassword').value : '';

    const payload = {
        nombre: nombreVal,
        Nombre: nombreVal,
        telefono: telefonoVal,
        Telefono: telefonoVal,
        tipoContrato: tipoContratoFinal,
        TipoContrato: tipoContratoFinal,
        valorContrato: valorContratoNum,
        ValorContrato: valorContratoNum,
        porcentajeComision: tipoContratoFinal === 'Porcentaje' ? valorContratoNum : null,
        PorcentajeComision: tipoContratoFinal === 'Porcentaje' ? valorContratoNum : null
    };

    if (emailVal !== '') {
        payload.emailParaUsuario = emailVal;
        payload.EmailParaUsuario = emailVal;
    }

    if (passVal !== '') {
        payload.passwordParaUsuario = passVal;
        payload.PasswordParaUsuario = passVal;
    }

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
                if (btn) btn.innerText = 'Subiendo imagen...';
                await subirFotoEmpleado(idEmpleado);
            }

            alert(esEdicion ? '🎉 Empleado actualizado con éxito.' : '🎉 Empleado y puesto de trabajo configurados con éxito.');
            cerrarModalStaff();
            listarPersonal();
        } else {
            let errorMsg = 'No se pudo guardar el empleado.';
            try {
                const errData = await response.json();
                if (errData.errors) {
                    errorMsg = Object.entries(errData.errors)
                        .map(([campo, msgs]) => `• ${campo}: ${msgs.join(', ')}`)
                        .join('\n');
                } else if (errData.message) {
                    errorMsg = errData.message;
                }
            } catch (pErr) {
                errorMsg = await response.text();
            }

            alert(`⚠️ Error al guardar empleado:\n${errorMsg}`);
        }
    } catch (err) {
        console.error("🚨 Error de red:", err);
        alert('❌ Error crítico de red al guardar el personal.');
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerText = origText;
        }
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
};

/* ============================================================================
   🪑 FLUJO 2: SILLAS / ESTACIONES DE TRABAJO (HU-001-B Y HU-001-C)
   ============================================================================ */
function renderizarBadgeVencimiento(fechaVencimiento, activo) {
    if (!activo || !fechaVencimiento) {
        return `<span class="badge badge-vencido">🔴 Vencida / Inactiva</span>`;
    }

    const fechaObj = new Date(fechaVencimiento);
    const hoy = new Date();
    
    const diffTime = fechaObj.getTime() - hoy.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    const fechaFormateada = fechaObj.toLocaleDateString('es-CO', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    });

    if (diffDays <= 0) {
        return `<span class="badge badge-vencido" title="Venció el ${fechaFormateada}">🔴 Vencida</span>`;
    } else if (diffDays <= 3) {
        return `<span class="badge badge-por-vencer" title="Vence el ${fechaFormateada}">🟡 Por Vencer (${diffDays}d)</span>`;
    } else {
        return `<span class="badge badge-activa" title="Vence el ${fechaFormateada}">🟢 Activa (${fechaFormateada})</span>`;
    }
}

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
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;">No hay estaciones configuradas.</td></tr>';
            return;
        }

        tbody.innerHTML = estaciones.map(est => {
            const nombreSilla = est.nombre || est.nombreSilla || 'Silla Sin Nombre';
            const tipoCobro = est.tipoCobro || 'Porcentaje';
            const valorBase = est.valorBase || 0;
            const valorTexto = tipoCobro === 'Porcentaje' ? `${valorBase}%` : `$${valorBase.toLocaleString('es-CO')}`;
            const periodicidad = est.periodicidad ? `<small style="display:block; opacity:0.7;">${est.periodicidad}</small>` : '';

            const badgeEstadoHtml = renderizarBadgeVencimiento(est.fechaVencimiento, est.activo);

            const accionesHtml = !tienePersonalOEquipo 
                ? `<span style="opacity:0.5; font-size:0.85rem;"><i class="fas fa-lock"></i> Solo Lectura</span>`
                : `
                    <button class="btn-pay-action" onclick="abrirModalActivar('${est.id}', '${nombreSilla.replace(/'/g, "\\'")}', ${valorBase})" title="Registrar Pago / Activar">
                        <i class="fas fa-cash-register"></i> Activar
                    </button>
                    <button class="btn-filter" onclick="eliminarEstacion('${est.id}')" style="background:#e94560; color:white; border:none; padding:6px 10px; border-radius:6px; cursor:pointer; margin-left:4px;" title="Eliminar">
                        <i class="fas fa-trash"></i>
                    </button>
                `;

            return `
                <tr>
                    <td><strong><i class="fas fa-chair" style="color:#48c1b5;"></i> ${nombreSilla}</strong></td>
                    <td>
                        <span class="status-pill status-pendiente">${tipoCobro}</span>
                        <strong style="margin-left:5px;">${valorTexto}</strong>
                    </td>
                    <td>
                        ${badgeEstadoHtml}
                        ${periodicidad}
                    </td>
                    <td><span class="status-pill ${est.activo ? 'status-activo' : 'status-bloqueado'}">${est.estado || (est.activo ? 'Disponible' : 'Inactiva')}</span></td>
                    <td>${accionesHtml}</td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        console.error(err);
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#e94560;">Error de comunicación con el servidor.</td></tr>';
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
};

window.cerrarModalEstacion = function() {
    document.getElementById('modalEstacion').style.display = 'none';
};

async function guardarEstacion(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarEstacion');
    if (btn) btn.disabled = true;

    const payload = {
        nombre: document.getElementById('estacionNombre').value.trim(),
        tipoCobro: document.getElementById('estacionTipoCobro')?.value || "Porcentaje",
        valorBase: parseFloat(document.getElementById('estacionValorBase')?.value || 0),
        estado: document.getElementById('estacionEstado')?.value || "Disponible"
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
            alert('🎉 Estación de trabajo creada con éxito.');
            cerrarModalEstacion();
            listarEstaciones();
        } else {
            alert('❌ Error al guardar la estación de trabajo.');
        }
    } catch (err) {
        console.error(err);
    } finally {
        if (btn) btn.disabled = false;
    }
}

window.abrirModalActivar = function(id, nombre, tarifaSugerida) {
    document.getElementById('actSillaId').value = id;
    document.getElementById('actSillaNombre').value = nombre;
    document.getElementById('actMonto').value = tarifaSugerida || 0;
    document.getElementById('actComprobante').value = '';
    document.getElementById('modalActivarSilla').style.display = 'flex';
};

window.cerrarModalActivar = function() {
    document.getElementById('modalActivarSilla').style.display = 'none';
};

window.procesarActivacionSilla = async function(e) {
    e.preventDefault();

    const id = document.getElementById('actSillaId').value;
    const btnSubmit = document.getElementById('btnConfirmarAct');

    const payload = {
        metodoPago: document.getElementById('actMetodoPago').value,
        periodo: document.getElementById('actPeriodo').value,
        monto: parseFloat(document.getElementById('actMonto').value) || 0,
        comprobante: document.getElementById('actComprobante').value.trim()
    };

    if (btnSubmit) {
        btnSubmit.disabled = true;
        btnSubmit.innerText = "Procesando...";
    }

    try {
        const response = await fetch(`${API_BASE}/EstacionesTrabajo/${id}/activar`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            alert("🚀 ¡Silla activada y renovada con éxito!");
            cerrarModalActivar();
            listarEstaciones();
        } else {
            const err = await response.json();
            alert("❌ Error: " + (err.message || "No se pudo activar la silla"));
        }
    } catch (error) {
        console.error("Error al activar silla:", error);
        alert("🔌 Error de conexión con el servidor.");
    } finally {
        if (btnSubmit) {
            btnSubmit.disabled = false;
            btnSubmit.innerHTML = '<i class="fas fa-check-circle"></i> Confirmar Activación';
        }
    }
};

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
};

/* ============================================================================
   FLUJO 3: MIS CLIENTES (WEB REGISTRADOS)
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
   💻 FLUJO 4: USUARIOS DEL SISTEMA (EXCLUSIVO SUPERADMIN / ADMIN - HU-DIR01 & HU-DIR02)
   ============================================================================ */
async function listarUsuariosSistema() {
    const tbody = document.getElementById('tablaUsuarios');
    if (!tbody) return;

    try {
        // Intentar invocar el nuevo endpoint protegido por RBAC (HU-DIR01)
        let response = await fetch(`${API_BASE}/v1/admin/system-users`, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json'
            }
        });

        // Fallback al endpoint global si el nuevo aún no está publicado
        if (!response.ok) {
            response = await fetch(`${API_BASE}/Usuarios`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
        }

        if (!response.ok) throw new Error("No fue posible consultar las cuentas de usuario.");

        const usuarios = await response.json();

        if (usuarios.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:#cbd5e1; padding: 15px;">No hay cuentas registradas en la plataforma.</td></tr>';
            return;
        }

        tbody.innerHTML = usuarios.map(usr => {
            const userId = usr.userId || usr.id;
            const email = usr.email || usr.Email || 'Sin email';
            const telefono = usr.telefono || usr.Telefono || 'Sin teléfono';
            const rolNombre = usr.rolNombre || usr.RolNombre || usr.rol || 'Usuario';
            const proveedorNombre = usr.proveedorNombre || usr.ProveedorNombre || 'N/A';
            const planNombre = usr.planNombre || usr.PlanNombre || 'Plan Estándar';

            const esActivo = usr.activo !== undefined ? usr.activo : !usr.estaBloqueado;
            const diasRestantes = usr.diasRestantes ?? 30;
            const esProximoAVencer = esActivo && diasRestantes <= 7;

            const badgeEstado = esActivo
                ? (esProximoAVencer 
                    ? `<span class="badge badge-por-vencer"><i class="fas fa-exclamation-triangle"></i> Próximo a Vencer (${diasRestantes}d)</span>`
                    : `<span class="badge badge-activa"><i class="fas fa-check-circle"></i> Activo</span>`)
                : `<span class="badge badge-vencido"><i class="fas fa-ban"></i> Inactivo / Bloqueado</span>`;

            const fechaVencRaw = usr.fechaVencimiento || usr.vencimientoSuscripcion;
            const fechaVencFmt = fechaVencRaw ? String(fechaVencRaw).split('T')[0] : 'N/A';

            return `
                <tr>
                    <td><strong>${usr.nombre}</strong></td>
                    <td>
                        <div><i class="fas fa-envelope" style="color:#38bdf8;"></i> ${email}</div>
                        <small style="color:#cbd5e1;"><i class="fas fa-phone"></i> ${telefono}</small>
                    </td>
                    <td><span class="status-pill status-pendiente" style="text-transform:uppercase;">${rolNombre}</span></td>
                    <td><strong>${proveedorNombre}</strong></td>
                    <td><span style="color:#38bdf8; font-weight:bold;">${planNombre}</span></td>
                    <td><small>${fechaVencFmt}</small></td>
                    <td>${badgeEstado}</td>
                    <td>
                        <div style="display:flex; gap:4px; align-items:center;">
                            <button onclick="abrirModalPagoManual('${userId}')" class="btn-pay-action" style="padding: 5px 9px; font-size: 0.8rem;" title="Registrar Pago Manual (Nequi / Daviplata / Consignación)">
                                <i class="fas fa-wallet"></i>
                            </button>
                            <button onclick="abrirModalProrroga('${userId}')" class="btn-filter" style="background:#f59e0b; color:#0f172a; border:none; padding:5px 9px; border-radius:6px; cursor:pointer; font-weight:bold; font-size: 0.8rem;" title="Ampliar Servicio (Prórroga Días)">
                                <i class="fas fa-clock"></i>
                            </button>
                            <button onclick="inactivarUsuarioSystem('${userId}')" class="btn-filter" style="background:${esActivo ? '#e94560' : '#10b981'}; color:white; border:none; padding:5px 9px; border-radius:6px; cursor:pointer; font-size: 0.8rem;" title="${esActivo ? 'Inactivar / Soft Delete' : 'Reactivar Cuenta'}">
                                <i class="fas ${esActivo ? 'fa-user-slash' : 'fa-user-check'}"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        console.error("❌ Error al listar usuarios del sistema:", err);
        tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:#e94560; padding:15px;">Error al obtener usuarios del sistema.</td></tr>';
    }
}

/* ============================================================================
   🚀 HU-DIR02: MANEJO DE MODALES DE REGISTRO MANUAL DE PAGOS Y PRÓRROGAS
   ============================================================================ */

window.abrirModalPagoManual = function(userId) {
    document.getElementById('pagoUserId').value = userId;
    document.getElementById('pagoReferencia').value = '';
    document.getElementById('pagoMonto').value = 70000;
    document.getElementById('pagoNotas').value = '';
    document.getElementById('modalPagoManual').style.display = 'flex';
};

window.cerrarModalPagoManual = function() {
    document.getElementById('modalPagoManual').style.display = 'none';
};

window.guardarPagoManual = async function(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarPagoManual');
    if (btn) btn.disabled = true;

    const userId = document.getElementById('pagoUserId').value;
    const payload = {
        paymentMethod: document.getElementById('pagoMedio').value,
        referenceNumber: document.getElementById('pagoReferencia').value.trim(),
        amount: parseFloat(document.getElementById('pagoMonto').value) || 0,
        extensionPeriodDays: parseInt(document.getElementById('pagoDias').value) || 30,
        notes: document.getElementById('pagoNotas').value.trim()
    };

    try {
        const response = await fetch(`${API_BASE}/v1/admin/subscriptions/${userId}/manual-payment`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            alert("🎉 ¡Pago manual registrado exitosamente! La suscripción fue extendida.");
            cerrarModalPagoManual();
            listarUsuariosSistema();
        } else {
            const err = await response.json();
            alert("❌ Error: " + (err.message || "No se pudo asentar el pago manual."));
        }
    } catch (err) {
        console.error("Error al registrar pago manual:", err);
        alert("🔌 Error de conexión al registrar el pago manual.");
    } finally {
        if (btn) btn.disabled = false;
    }
};

window.abrirModalProrroga = function(userId) {
    document.getElementById('prorrogaUserId').value = userId;
    document.getElementById('prorrogaDias').value = 7;
    document.getElementById('prorrogaMotivo').value = '';
    document.getElementById('modalProrroga').style.display = 'flex';
};

window.cerrarModalProrroga = function() {
    document.getElementById('modalProrroga').style.display = 'none';
};

window.guardarProrroga = async function(e) {
    e.preventDefault();
    const btn = document.getElementById('btnGuardarProrroga');
    if (btn) btn.disabled = true;

    const userId = document.getElementById('prorrogaUserId').value;
    const payload = {
        additionalDays: parseInt(document.getElementById('prorrogaDias').value) || 1,
        reason: document.getElementById('prorrogaMotivo').value.trim()
    };

    try {
        const response = await fetch(`${API_BASE}/v1/admin/subscriptions/${userId}/extend-service`, {
            method: 'PATCH',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            alert("🎉 ¡Prórroga otorgada exitosamente!");
            cerrarModalProrroga();
            listarUsuariosSistema();
        } else {
            const err = await response.json();
            alert("❌ Error: " + (err.message || "No se pudo otorgar la prórroga."));
        }
    } catch (err) {
        console.error("Error al aplicar prórroga:", err);
        alert("🔌 Error de red al aplicar la prórroga.");
    } finally {
        if (btn) btn.disabled = false;
    }
};

window.inactivarUsuarioSystem = async function(userId) {
    if (!confirm("¿Deseas cambiar el estado de acceso de esta cuenta en el sistema?")) return;

    try {
        const response = await fetch(`${API_BASE}/v1/admin/users/${userId}`, {
            method: 'DELETE',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json'
            }
        });

        if (response.ok) {
            const resData = await response.json();
            alert(`🚀 ${resData.message || 'Estado de la cuenta actualizado.'}`);
            listarUsuariosSistema();
        } else {
            let errorMsg = "No se pudo cambiar el estado de la cuenta.";
            try {
                const err = await response.json();
                if (err.message) errorMsg = err.message;
            } catch (pErr) {}
            alert(`❌ Error: ${errorMsg}`);
        }
    } catch (err) {
        console.error("Error al cambiar estado del usuario:", err);
        alert("🔌 Error de conexión con el servidor.");
    }
};

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
};