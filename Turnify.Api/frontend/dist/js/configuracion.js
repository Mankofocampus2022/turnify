/* =========================================
   TURNIFY - LÓGICA MAESTRA (DASHBOARD + CONFIG)
   ========================================= */

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
        // Estilo turquesa Heineken para tu nombre
        welcomeText.innerHTML = `¡Qué más, <span style="color: #48c1b5;">${nombreFinal}</span>!`;
    }

    // --- 3. GESTIÓN DE TABS (Si estás en Configuración) ---
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
                    }
                }
            });
        });
    }

    // --- 4. CARGA INICIAL DE DATOS ---
    // Si existe el ID de proveedor, cargamos su perfil
    if(proveedorId) {
        cargarDatosConfig(proveedorId, token);
    }

    // Si existen botones de filtro, disparamos la agenda de 'Hoy'
    const btnHoy = document.querySelector(".btn-filter");
    if(btnHoy) cambiarPeriodo('hoy', btnHoy);

    // Si existe el formulario de perfil, activamos el evento de guardado
    const formPerfil = document.getElementById('formConfigPerfil');
    if(formPerfil) {
        formPerfil.addEventListener('submit', (e) => guardarConfig(e, proveedorId, token));
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
        const response = await fetch(`http://localhost:5000/api/Citas/rango?inicio=${startStr}&fin=${endStr}`, {
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

    // 🚩 ORDEN SOLICITADO: HORA | FECHA | CLIENTE | SERVICIO | ESTADO
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
   SECCIÓN: PERFIL Y HORARIOS
   ========================================= */

async function cargarDatosConfig(proveedorId, token) {
    try {
        const response = await fetch(`http://localhost:5000/api/Proveedores/${proveedorId}`, {
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

    const body = {
        Id: proveedorId,
        NombreComercial: document.getElementById('negocioNombre').value.trim(),
        Direccion: document.getElementById('negocioDireccion').value.trim(),
        Tipo: document.getElementById('negocioTipo') ? document.getElementById('negocioTipo').value : "Barbería" 
    };

    try {
        const response = await fetch(`http://localhost:5000/api/Proveedores/${proveedorId}`, {
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
        const response = await fetch('http://localhost:5000/api/Horarios/mi-semana', {
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
        const response = await fetch('http://localhost:5000/api/Horarios/configurar-semana', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify(horarios)
        });
        if (response.ok) alert("✅ ¡Horarios sincronizados!");
    } catch (error) { console.error(error); }
}

function getEstadoClass(estado) {
    if (estado.includes('completado') || estado.includes('confirmada')) return 'status-activo';
    if (estado.includes('cancelada') || estado.includes('suspendido')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

function logout() {
    if (confirm("¿Se va a abrir, mi perro? Guarde todo antes de salir.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}