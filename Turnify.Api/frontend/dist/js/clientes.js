document.addEventListener('DOMContentLoaded', () => {
    // 🚩 CONFIGURACIÓN DINÁMICA (Blindaje contra entornos fijos)
    const API_BASE_URL = window.location.origin + '/api'; 
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    const rol = localStorage.getItem('usuario_rol');
    const rolesPermitidos = ['Administrador', 'SuperAdmin', 'Cliente'];

    if (!rolesPermitidos.includes(rol)) {
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
    // 🚩 REDEFINICIÓN DE URL (Blindaje para Docker/Producción)
    const API_BASE_URL = window.location.origin + '/api'; 
    const token = localStorage.getItem('token');

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
 */
function verHistorial(clienteId) {
    // 🚩 Redirigir o abrir modal para que el barbero vea las citas del cliente y le de su token si lo perdió
    console.log("Consultando historial del cliente:", clienteId);
    // Podrías abrir un modal que llame a api/Citas/historial/{clienteId}
    alert("Consultando historial... Aquí podrás ver los códigos de verificación del cliente.");
}