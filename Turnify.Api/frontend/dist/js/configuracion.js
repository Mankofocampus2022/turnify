/* ============================================================
   TURNIFY - MOTOR DE AGENDAMIENTO Y DISPONIBILIDAD HORARIA
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el host en caliente. Si entras desde localhost usa el puerto 5000, 
// si entras desde otra IP de la red local o dominio, reconfigura el endpoint automáticamente para toda la suite de funciones.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : `${window.location.protocol}//${window.location.hostname}:5000`;

const API_BASE = `${API_HOST}/api`;

document.addEventListener('DOMContentLoaded', () => {
    // --- 1. PUENTE DE SEGURIDAD (Versión Blindada) ---
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const userStr = localStorage.getItem('user');
    let proveedorId = localStorage.getItem('proveedorId') || localStorage.getItem('proveedor_id');
    
    // Si no está el ID directo, lo extraemos del objeto usuario
    if (!proveedorId && userStr) {
        try {
            const userObj = JSON.parse(userStr);
            proveedorId = userObj.proveedorId || userObj.id; 
        } catch (e) { console.error("❌ Error al parsear objeto usuario"); }
    }

    // Validación contra valores nulos o corruptos
    if (proveedorId === "null" || proveedorId === "undefined" || !proveedorId) {
        proveedorId = null;
    }

    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    const esSuperAdmin = rol.includes("ADMIN");

    if (!token || (!proveedorId && !esSuperAdmin)) {
        console.error("🚫 Sesión inválida. Redirigiendo...");
        if(!token) {
            localStorage.clear();
            window.location.href = 'login.html';
        }
        return;
    }

    console.log("✅ Sesión activa para:", proveedorId);

    // --- 2. SALUDO PERSONALIZADO (Recuadro del Logo) ---
    let nombreFinal = "Darwin"; 
    if (userStr) {
        try {
            const userObj = JSON.parse(userStr);
            nombreFinal = userObj.nombre || userObj.Nombre || nombreFinal;
        } catch (e) { console.error("Error al cargar nombre"); }
    }

    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) {
        welcomeText.innerHTML = `¡Qué más, <span style="color: #48c1b5;">${nombreFinal}</span>!`;
    }

    // --- 3. GESTIÓN DE TABS ---
    const menuItems = document.querySelectorAll('.config-menu-item');
    const sections = document.querySelectorAll('.config-content');

    if (menuItems.length > 0) {
        menuItems.forEach((item, index) => {
            item.addEventListener('click', () => {
                menuItems.forEach(i => i.classList.remove('active'));
                item.classList.add('active');
                sections.forEach(s => s.style.display = 'none');
                
                const sectionIds = ['content-perfil', 'content-horarios', 'content-pagos', 'content-notificaciones'];
                const targetId = sectionIds[index];
                
                if(targetId) {
                    const targetElement = document.getElementById(targetId);
                    if(targetElement) {
                        targetElement.style.display = 'block';
                        if(targetId === 'content-horarios') cargarHorarios();
                        // 🧠 ADICIÓN: Al dar clic en pestañas de pagos o notificaciones, disparamos su carga asíncrona
                        if(targetId === 'content-pagos') cargarDatosPagos(proveedorId, token);
                        if(targetId === 'content-notificaciones') cargarDatosNotificaciones(proveedorId, token);
                    }
                }
            });
        });
    }

    // --- 4. CARGA INICIAL DE DATOS ---
    if(proveedorId) {
        cargarDatosConfig(proveedorId, token);
        // 🚩 ADICIÓN SENIOR: Generar QR al iniciar si existe el ID
        setTimeout(() => generarQRNegocio(proveedorId), 500);
    }

    const btnHoy = document.querySelector(".btn-filter");
    if(btnHoy) cambiarPeriodo('hoy', btnHoy);

    const formPerfil = document.getElementById('formConfigPerfil');
    if(formPerfil) {
        formPerfil.addEventListener('submit', (e) => guardarConfig(e, proveedorId, token));
    }

    // 🧠 ADICIÓN: Vinculación de escucha de eventos de envío para los nuevos módulos fintech y alertas
    const formPagos = document.getElementById('formConfigPagos');
    if(formPagos) {
        formPagos.addEventListener('submit', (e) => guardarConfigPagos(e, proveedorId, token));
    }

    const formNotif = document.getElementById('formConfigNotificaciones');
    if(formNotif) {
        formNotif.addEventListener('submit', (e) => guardarConfigNotificaciones(e, proveedorId, token));
    }
});

/* =========================================
   SECCIÓN: AGENDA Y RENDERIZADO
   ========================================= */

async function cambiarPeriodo(periodo, boton) {
    if (!boton) return;
    document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
    boton.classList.add('active');

    const titulos = { 'hoy': 'Agenda de Hoy', 'mañana': 'Agenda de Mañana', 'semana': 'Agenda de la Semana', 'mes': 'Agenda del Mes' };
    const sectionTitle = document.getElementById('sectionTitle');
    if (sectionTitle) sectionTitle.innerText = titulos[periodo];

    let inicio = new Date();
    let fin = new Date();
    if (periodo === 'mañana') { inicio.setDate(inicio.getDate() + 1); fin.setDate(fin.getDate() + 1); }
    else if (periodo === 'semana') { fin.setDate(fin.getDate() + 7); }
    else if (periodo === 'mes') { fin.setMonth(fin.getMonth() + 1); }

    const startStr = inicio.toISOString().split('T')[0];
    const endStr = fin.toISOString().split('T')[0];
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    try {
        // 🚩 FIX DE URL DOCKER: Reemplazado localhost por la constante dinámica centralizada
        const response = await fetch(`${API_BASE}/Citas/rango?inicio=${startStr}&fin=${endStr}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const citas = await response.json();
            renderizarTablaDashboard(citas);
        }
    } catch (error) { console.error("🔥 Error agenda:", error); }
}

function renderizarTablaDashboard(citas) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla || !citas) return;

    if (citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="5" style="text-align: center; padding: 20px;">No hay citas agendadas.</td></tr>';
        return;
    }

    tabla.innerHTML = citas.map(c => {
        const estado = (c.estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        const fechaObj = new Date(c.fecha + 'T00:00:00');
        const fechaFormateada = fechaObj.toLocaleDateString('es-CO', { day: '2-digit', month: 'short' });

        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora}</td>
                <td style="opacity: 0.8;">${fechaFormateada}</td>
                <td><strong>${c.clienteNombre || 'Sin nombre'}</strong></td>
                <td>${c.servicioNombre || 'Servicio'}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
            </tr>
        `;
    }).join('');
}

/* =========================================
   SECCIÓN: PERFIL, HORARIOS Y QR (THE BOSS)
   ========================================= */

// 🚩 NUEVA FUNCIÓN: Generación de QR Dinámico
function generarQRNegocio(proveedorId) {
    // 🚩 FIX: Apuntando al archivo correcto agendar-cita.html
    const urlReserva = `${window.location.origin}/agendar-cita.html?id=${proveedorId}`;
    const container = document.getElementById('qr-container');
    
    if (!container) return; // Si no existe el div en el HTML, no hace nada

    const qrUrl = `https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=${encodeURIComponent(urlReserva)}&color=48c1b5&bgcolor=0a101e`;

    container.innerHTML = `
        <div style="text-align: center; padding: 20px; background: rgba(72,193,181,0.05); border: 1px solid rgba(72,193,181,0.2); border-radius: 20px;">
            <img src="${qrUrl}" alt="QR Turnify" id="img-qr" style="border: 5px solid #48c1b5; border-radius: 15px; margin: 0 auto;">
            <p style="margin-top: 15px; color: #48c1b5; font-weight: 800; font-size: 14px;">CLIENTES ESCANEAN AQUÍ</p>
            <button onclick="descargarQR('${qrUrl}')" class="btn-save" style="margin-top: 10px; width: auto; padding: 10px 20px;">
                <i class="fas fa-download"></i> Descargar QR
            </button>
        </div>
    `;
}

// 🚩 NUEVA FUNCIÓN: Descarga del QR para impresión
async function descargarQR(url) {
    try {
        const response = await fetch(url);
        const blob = await response.blob();
        const fileUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = fileUrl;
        link.download = `QR_Turnify_Negocio.png`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (e) { alert("Error al descargar el QR"); }
}

async function cargarDatosConfig(proveedorId, token) {
    try {
        // 🚩 FIX DE URL DOCKER: Reemplazado localhost por la constante dinámica centralizada
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const data = await response.json();
            if(document.getElementById('negocioNombre')) document.getElementById('negocioNombre').value = data.nombreComercial || data.nombre || '';
            if(document.getElementById('negocioEmail')) document.getElementById('negocioEmail').value = data.email || '';
            if(document.getElementById('negocioTelefono')) document.getElementById('negocioTelefono').value = data.telefono || '';
            if(document.getElementById('negocioDireccion')) document.getElementById('negocioDireccion').value = data.direccion || '';
            if(document.getElementById('negocioTipo')) document.getElementById('negocioTipo').value = data.tipo || 'Barbería';
        }
    } catch (error) { console.error(error); }
}

async function guardarConfig(e, proveedorId, token) {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando...';

    // 🚩 KILLER FIX FRONTEND: Capturamos los elementos de teléfono y correo para inyectarlos en el request
    const inputTelefono = document.getElementById('negocioTelefono');
    const inputEmail = document.getElementById('negocioEmail');
    const tipoSelect = document.getElementById('negocioTipo') ? document.getElementById('negocioTipo').value : "Barbería";

    // 🧠 SINCRONIZACIÓN MULTI-TENANT: Mapeamos dinámicamente los valores hacia el discriminador exacto del Bot
    let categoriaMapeada = "Barbero";
    if (tipoSelect === "Manicure") {
        categoriaMapeada = "Manicurista";
    } else if (tipoSelect === "Estética") {
        categoriaMapeada = "Estética";
    }

    const body = {
        Id: proveedorId,
        NombreComercial: document.getElementById('negocioNombre').value.trim(),
        Direccion: document.getElementById('negocioDireccion').value.trim(),
        Tipo: tipoSelect,
        Categoria: categoriaMapeada, // 🚩 Inyectado seguro para evitar cruce de cables con Postgres
        Telefono: inputTelefono ? inputTelefono.value.trim() : "",
        Email: inputEmail ? inputEmail.value.trim() : ""
    };

    try {
        // 🚩 FIX DE URL DOCKER: Reemplazado localhost por la constante dinámica centralizada
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}`, {
            method: 'PUT', 
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(body)
        });
        if (response.ok) alert("✅ ¡Perfil actualizado, mi perro!");
    } catch (error) { alert("🚀 Error de conexión."); }
    finally { btn.disabled = false; btn.innerHTML = originalHTML; }
}

async function cargarHorarios() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const dias = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];
    const contenedor = document.getElementById('lista-horarios');
    if(!contenedor) return;
    
    try {
        // 🚩 FIX DE URL DOCKER: Reemplazado localhost por la constante dinámica centralizada
        const response = await fetch(`${API_BASE}/Horarios/mi-semana`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        let horariosGuardados = [];
        if (response.ok) horariosGuardados = await response.json();

        contenedor.innerHTML = dias.map((dia, i) => {
            const h = horariosGuardados.find(x => x.diaSemana === i);
            const open = h ? h.horaApertura.slice(0, 5) : "08:00";
            const close = h ? h.horaCierre.slice(0, 5) : "20:00";
            const isClosed = h && h.horaApertura === "00:00:00" && h.horaCierre === "00:00:00";
            return `
                <div class="horario-row" style="display: flex; gap: 15px; margin-bottom: 15px; align-items: center; background: #122940; padding: 12px; border-radius: 10px; border: 1px solid rgba(72,193,181,0.2);">
                    <div style="width: 100px; color: #48c1b5;"><strong>${dia}</strong></div>
                    <input type="time" id="open-${i}" value="${open}" style="background: #1b3d5f; color: white; border: none; padding: 5px;">
                    <span style="color: white;">a</span>
                    <input type="time" id="close-${i}" value="${close}" style="background: #1b3d5f; color: white; border: none; padding: 5px;">
                    <label style="color: #e94560; cursor: pointer;"><input type="checkbox" id="closed-${i}" ${isClosed ? 'checked' : ''}> Cerrado</label>
                </div>`;
        }).join('');
        const btnSaveH = document.querySelector('#content-horarios .btn-save');
        if(btnSaveH) btnSaveH.onclick = guardarTodosLosHorarios;
    } catch (error) { console.error(error); }
}

async function guardarTodosLosHorarios() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const horarios = [];
    for (let i = 0; i < 7; i++) {
        const check = document.getElementById(`closed-${i}`);
        if(check) {
            horarios.push({
                DiaSemana: i,
                HoraApertura: check.checked ? "00:00" : document.getElementById(`open-${i}`).value,
                HoraCierre: check.checked ? "00:00" : document.getElementById(`close-${i}`).value
            });
        }
    }
    try {
        // 🚩 FIX DE URL DOCKER: Reemplazado localhost por la constante dinámica centralizada
        const response = await fetch(`${API_BASE}/Horarios/configurar-semana`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(horarios)
        });
        if (response.ok) alert("✅ ¡Horarios sincronizados!");
    } catch (error) { console.error(error); }
}

/* ============================================================
   🧠 NUEVAS SECCIONES FINTECH (NEQUI, DAVIPLATA) Y NOTIFICACIONES
   ============================================================ */

async function cargarDatosPagos(proveedorId, token) {
    try {
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}/pagos`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const data = await response.json();
            if(document.getElementById('pagoNequi')) document.getElementById('pagoNequi').value = data.nequiCelular || '';
            if(document.getElementById('pagoDaviplata')) document.getElementById('pagoDaviplata').value = data.daviplataCelular || '';
            if(document.getElementById('pagoBancoNombre')) document.getElementById('pagoBancoNombre').value = data.bancoNombre || '';
            if(document.getElementById('pagoTipoCuenta')) document.getElementById('pagoTipoCuenta').value = data.bancoTipo || 'Ahorros';
            if(document.getElementById('pagoNumeroCuenta')) document.getElementById('pagoNumeroCuenta').value = data.bancoNumero || '';
        }
    } catch (error) { console.error("Error leyendo datos de recaudo digital:", error); }
}

async function guardarConfigPagos(e, proveedorId, token) {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Sincronizando cuentas...';

    const body = {
        ProveedorId: proveedorId,
        NequiCelular: document.getElementById('pagoNequi').value.trim(),
        DaviplataCelular: document.getElementById('pagoDaviplata').value.trim(),
        BancoNombre: document.getElementById('pagoBancoNombre').value.trim(),
        BancoTipo: document.getElementById('pagoTipoCuenta').value,
        BancoNumero: document.getElementById('pagoNumeroCuenta').value.trim()
    };

    try {
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}/pagos`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(body)
        });
        if (response.ok) alert("✅ ¡Pasarela digital vinculada, mi perro!");
    } catch (error) { alert("🚀 Error al inyectar datos de recaudo."); }
    finally { btn.disabled = false; btn.innerHTML = originalHTML; }
}

async function cargarDatosNotificaciones(proveedorId, token) {
    try {
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}/notificaciones`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const data = await response.json();
            if(document.getElementById('notifWhatsApp')) document.getElementById('notifWhatsApp').checked = data.permitirWhatsApp ?? true;
            if(document.getElementById('notifEmail')) document.getElementById('notifEmail').checked = data.permitirEmail ?? true;
        }
    } catch (error) { console.error("Error leyendo configuración de alertas:", error); }
}

async function guardarConfigNotificaciones(e, proveedorId, token) {
    e.preventDefault();
    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando alertas...';

    const body = {
        ProveedorId: proveedorId,
        PermitirWhatsApp: document.getElementById('notifWhatsApp').checked,
        PermitirEmail: document.getElementById('notifEmail').checked
    };

    try {
        const response = await fetch(`${API_BASE}/Proveedores/${proveedorId}/notificaciones`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(body)
        });
        if (response.ok) alert("✅ ¡Preferencias de alertas configuradas con éxito!");
    } catch (error) { alert("🚀 Error al guardar canales de comunicación."); }
    finally { btn.disabled = false; btn.innerHTML = originalHTML; }
}

/* ============================================================
   SECCIÓN: UTILIDADES GENERALES
   ============================================================ */

function getEstadoClass(estado) {
    if (estado.includes('completado') || estado.includes('confirmada')) return 'status-activo';
    if (estado.includes('cancelada') || estado.includes('suspendido')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

function logout() {
    if (confirm("¿Seguro que te vas a salir? te extrañaremos mucho hasta que vuelvas.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}