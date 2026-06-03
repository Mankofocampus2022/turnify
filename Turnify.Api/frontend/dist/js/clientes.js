/* ============================================================
   TURNIFY - MOTOR DE GESTIÓN Y SEGUIMIENTO DE CLIENTES
   ============================================================ */

document.addEventListener('DOMContentLoaded', () => {
    // 🚩 CONFIGURACIÓN DINÁMICA (Blindaje contra entornos fijos - Matriz de Red Inteligente)
    let API_BASE_URL = window.location.origin + '/api';
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE_URL = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }

    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();
    
    // GUIDs de administración centralizada de la base de datos SQL Server
    const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";
    const ADMIN_GUID = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";
    const BARBERO_GUID = "8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7";

    // 🧠 FIX DE PRIVILEGIOS: Aseguramos compatibilidad con Strings y GUIDs de base de datos
    const esValido = rol.includes("ADMIN") || 
                     rol.includes("SUPERADMIN") || 
                     rol.includes("BARBERO") || 
                     rol.includes("PROVEEDOR") ||
                     rol.includes("CLIENTE") ||
                     rol === SUPER_ADMIN_GUID || 
                     rol === ADMIN_GUID || 
                     rol === BARBERO_GUID;

    if (!esValido) {
        window.location.href = 'login.html';
        return;
    }

    // 🔍 Inyección de Motor de Búsqueda (Senior UX)
    const inputBusqueda = document.getElementById('search-clientes');
    if (inputBusqueda) {
        inputBusqueda.addEventListener('input', (e) => filtrarClientes(e.target.value));
    }

    cargarClientes();
});

// Variable global para el filtro
window.listadoCompletoClientes = [];

async function cargarClientes() {
    // 🚩 REDEFINICIÓN DE URL (Blindaje para Docker/Producción - Sincronizado)
    let API_BASE_URL = window.location.origin + '/api';
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE_URL = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }

    // 🧠 FIX: Doble asignación preventiva para evitar cabeceras Authorization con strings "null"
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    try {
        // 🛡️ PETICIÓN SEGURA: Añadimos el Header de Authorization
        const response = await fetch(`${API_BASE_URL}/Clientes`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        }); 
        
        if (response.ok) {
            const clientes = await response.json();
            window.listadoCompletoClientes = clientes; // Guardamos para el filtro
            renderizarLista(clientes);
        } else {
            console.error("⚠️ Error de permisos o sesión expirada");
        }
    } catch (error) {
        console.error("❌ Error crítico de conexión:", error);
        const tabla = document.getElementById('lista-clientes');
        if (tabla) tabla.innerHTML = '<tr><td colspan="4" style="text-align:center; color:#ff5e5e;">Error de conexión con el servidor</td></tr>';
    }
}

/**
 * 📝 FUNCIÓN DE RENDERIZADO (Diseño Neón / Glass)
 */
function renderizarLista(clientes) {
    const tabla = document.getElementById('lista-clientes');
    if (!tabla) return;

    tabla.innerHTML = ''; 

    if (clientes.length === 0) {
        tabla.innerHTML = '<tr><td colspan="4" style="text-align:center; padding:20px;">No se encontraron clientes registrados.</td></tr>';
        return;
    }

    clientes.forEach(cliente => {
        // 🛡️ Lógica de visualización de estados (Sincronizada con CSS)
        const badgeClass = cliente.activo ? 'status-activo' : 'status-bloqueado';
        const textoEstado = cliente.activo ? '● Activo' : '○ Inactivo';

        const fila = `
            <tr style="border-bottom: 1px solid rgba(72, 193, 181, 0.1);">
                <td style="padding: 15px;">
                    <div style="font-weight: 600; color: #48c1b5;">${cliente.nombre}</div>
                    <div style="font-size: 11px; color: #888;">ID: ${cliente.id.substring(0,8)}...</div>
                </td>
                <td style="padding: 15px;">
                    <div style="font-size: 13px; color: #ccc;">${cliente.email}</div>
                    <div style="font-size: 12px; color: #48c1b5; font-weight: bold;">
                        <i class="fas fa-phone-alt" style="font-size: 10px;"></i> ${cliente.telefono || 'Sin registro'}
                    </div>
                </td>
                <td style="padding: 15px;">
                    <span class="status-pill ${badgeClass}" style="font-size: 10px; padding: 4px 10px;">
                        ${textoEstado}
                    </span>
                </td>
                <td style="text-align: right; padding: 15px;">
                    <button class="btn-checkin-report" style="padding: 6px 12px; font-size: 0.65rem;" onclick="verHistorial('${cliente.id}')">
                        <i class="fas fa-history"></i> VER CITAS
                    </button>
                    <button class="btn-action" style="margin-left: 5px; background: transparent; border: 1px solid #888; color: #888; padding: 6px 10px; border-radius: 6px; cursor: pointer; font-size: 0.65rem;">
                        <i class="fas fa-edit"></i>
                    </button>
                </td>
            </tr>
        `;
        tabla.innerHTML += fila;
    });
}

/**
 * 🔍 FILTRO DINÁMICO (Sin recargar la página)
 */
function filtrarClientes(termino) {
    const filtrados = window.listadoCompletoClientes.filter(c => 
        c.nombre.toLowerCase().includes(termino.toLowerCase()) || 
        c.email.toLowerCase().includes(termino.toLowerCase())
    );
    renderizarLista(filtrados);
}

/**
 * 🔑 FUNCIÓN PARA AYUDAR AL CLIENTE (Ver sus tokens)
 * (Versión funcional al 100% integrada al CitasController de .NET Core)
 */
async function verHistorial(clienteId) {
    console.log("Consultando historial del cliente:", clienteId);
    
    let API_BASE_URL = window.location.origin + '/api';
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE_URL = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }

    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    try {
        // Disparamos la consulta directo al endpoint transaccional del historial
        const response = await fetch(`${API_BASE_URL}/Citas/historial/${clienteId}`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json'
            }
        });

        if (response.ok) {
            const citas = await response.json();
            
            if (!citas || citas.length === 0) {
                alert("ℹ️ Este cliente no registra agendas de citas en el sistema.");
                return;
            }

            // Mapeamos el vector de datos con formato limpio para legibilidad del Profesional
            const infoCitas = citas.map(c => {
                const fechaLimpia = c.fecha ? c.fecha.split('T')[0] : 'Hoy';
                const horaLimpia = c.hora ? c.hora.toString().slice(0, 5) : '--:--';
                return `📅 Fecha: ${fechaLimpia} | ⏰ Hora: ${horaLimpia}\n` +
                       `🛠️ Servicio: ${c.servicioNombre || 'Servicio'}\n` +
                       `📌 Estado: ${c.estado.toUpperCase()}\n` +
                       `🔑 TOKEN CHECK-IN: ${c.codigoVerificacion || 'Ninguno'}\n` +
                       `----------------------------------------`;
            }).join('\n');

            alert(`📋 HISTORIAL DE CITAS Y TOKENS RECONOCIDOS:\n\n${infoCitas}`);
        } else {
            alert("❌ No se pudo recuperar el historial. Verifica la sesión del usuario.");
        }
    } catch (error) {
        console.error("🔥 Error de red al traer historial:", error);
        alert("🔌 Error de red al conectar con el servidor.");
    }
}

// Vinculamos la función al puente global de la ventana
window.verHistorial = verHistorial;