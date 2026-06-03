/* ============================================================
   TURNIFY - MOTOR DE ADMINISTRACIÓN CENTRAL DE PLATAFORMA
   ============================================================ */

// 1. CONFIGURACIÓN CENTRALIZADA Y SEGURIDAD CRÍTICA
// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el host en caliente. Si entras desde localhost usa el puerto 5000, 
// si entras desde otra IP de la red local (ej: 192.168.x.x) o dominio, reconfigura el endpoint automáticamente.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : `${window.location.protocol}//${window.location.hostname}:5000`;

const API_BASE = `${API_HOST}/api`;
const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";

// El "Portero": Si no hay token o no es el rol correcto, ¡fuera!
const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
const userRole = (localStorage.getItem('usuario_rol') || "").toUpperCase();

// 🧠 FIX DE CONTROL DE ACCESO: Validamos por GUID o por el nombre string del Rol para evitar bloqueos por mapeo
if (!token || (userRole !== SUPER_ADMIN_GUID && !userRole.includes("ADMIN") && !userRole.includes("SUPER"))) {
    alert("⛔ Acceso denegado. No tienes permisos para estar aquí.");
    window.location.href = 'login.html';
}

// Generador de Headers para no repetir código
const getHeaders = () => ({
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
    'Accept': 'application/json'
});

// 2. INICIALIZACIÓN AL CARGAR EL DOM
document.addEventListener('DOMContentLoaded', () => {
    const nombre = localStorage.getItem('usuario_nombre') || "Administrador";
    
    // Saludo personalizado
    const welcomeElement = document.getElementById('welcome-text');
    if (welcomeElement) welcomeElement.innerText = `Hola, ${nombre}`;
    
    // Carga inicial de datos
    cargarKPIs();

    // 3. EVENTOS DE NAVEGACIÓN
    document.getElementById('nav-dashboard')?.addEventListener('click', () => {
        actualizarMenuActivo('nav-dashboard');
        volverAlDashboard();
    });

    document.getElementById('nav-negocios')?.addEventListener('click', () => {
        actualizarMenuActivo('nav-negocios');
        switchView('section-negocios');
        cargarTablaProveedores();
    });

    document.getElementById('nav-usuarios')?.addEventListener('click', () => {
        actualizarMenuActivo('nav-usuarios');
        mostrarSeccionUsuarios();
    });

    document.getElementById('nav-logout')?.addEventListener('click', logout);
});

// --- FUNCIONES DE CARGA DE DATOS ---

async function cargarKPIs() {
    try {
        // Nota: Asegúrate de que este endpoint exista en tu API
        const response = await fetch(`${API_BASE}/Usuarios/dashboard-stats`, {
            headers: getHeaders()
        });
        
        if (response.ok) {
            const data = await response.json();
            document.getElementById('stat-proveedores').innerText = data.proveedoresCount || 0;
            document.getElementById('stat-usuarios').innerText = data.usuariosCount || 0;
            document.getElementById('stat-ingresos').innerText = `$${data.ingresosMensuales || 0}`;
        }
    } catch (error) {
        console.error("❌ Error cargando estadísticas:", error);
    }
}

async function cargarTablaUsuarios() {
    try {
        const response = await fetch(`${API_BASE}/Usuarios`, {
            headers: getHeaders()
        });
        const usuarios = await response.json();
        const tbody = document.getElementById('tabla-usuarios-body');
        if (!tbody) return;
        
        tbody.innerHTML = usuarios.map(user => `
            <tr class="border-b hover:bg-gray-50">
                <td class="p-3 text-sm">${user.id.substring(0, 8)}...</td>
                <td class="p-3 font-medium">${user.nombre}</td>
                <td class="p-3">${user.email}</td>
                <td class="p-3"><span class="px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-xs">${user.rol || 'Sin Rol'}</span></td>
                <td class="p-3">
                    <button class="text-blue-600 hover:text-blue-900 mr-2" onclick="editarUsuario('${user.id}')" title="Editar">✏️</button>
                    <button class="text-red-600 hover:text-red-900" onclick="eliminarUsuario('${user.id}')" title="Eliminar">🗑️</button>
                </td>
            </tr>`).join('');
    } catch (error) {
        console.error("❌ Error al cargar usuarios:", error);
    }
}

// 🧠 [NUEVO] IMPLEMENTACIÓN COMPLETA DE TABLA DE PROVEEDORES / NEGOCIOS
async function cargarTablaProveedores() {
    try {
        const response = await fetch(`${API_BASE}/Proveedores`, {
            headers: getHeaders()
        });
        if (!response.ok) return;
        const proveedores = await response.json();
        const tbody = document.getElementById('tabla-negocios-body') || document.getElementById('tabla-proveedores-body');
        if (!tbody) return;
        
        tbody.innerHTML = proveedores.map(p => `
            <tr class="border-b hover:bg-gray-50">
                <td class="p-3 text-sm">${p.id.substring(0, 8)}...</td>
                <td class="p-3 font-medium">${p.nombreComercial || p.nombre}</td>
                <td class="p-3">${p.email || 'N/A'}</td>
                <td class="p-3">${p.telefono || 'N/A'}</td>
                <td class="p-3">
                    <span class="px-2 py-1 bg-green-100 text-green-800 rounded-full text-xs">Activo</span>
                </td>
            </tr>`).join('');
    } catch (error) {
        console.error("❌ Error al cargar proveedores:", error);
    }
}

// --- ACCIONES Y NAVEGACIÓN ---

function logout() {
    if(confirm("¿Seguro que quieres cerrar sesión en Turnify?")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}

function switchView(viewId) {
    const sections = ['section-stats', 'section-usuarios', 'section-negocios', 'section-config'];
    sections.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.classList.toggle('hidden', id !== viewId);
    });
}

function volverAlDashboard() { switchView('section-stats'); cargarKPIs(); }
function mostrarSeccionUsuarios() { switchView('section-usuarios'); cargarTablaUsuarios(); }

function actualizarMenuActivo(id) {
    document.querySelectorAll('.nav-links li').forEach(b => b.classList.remove('bg-blue-700', 'active'));
    document.getElementById(id)?.classList.add('bg-blue-700', 'active');
}

// 🧠 [NUEVO] FUNCIONES INTERNAS CRUD PARA OPERACIÓN DE MANTENIMIENTO DESDE LA UI
async function editarUsuario(id) {
    const nuevoNombre = prompt("Ingresa el nuevo nombre para este usuario:");
    if (!nuevoNombre || nuevoNombre.trim() === "") return;

    try {
        const resp = await fetch(`${API_BASE}/Usuarios/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify({ id: id, nombre: nuevoNombre.trim() })
        });
        if (resp.ok) {
            alert("✅ Usuario actualizado correctamente.");
            cargarTablaUsuarios();
        } else {
            alert("❌ No se pudo actualizar el usuario.");
        }
    } catch (e) { console.error("Error editando usuario:", e); }
}

async function eliminarUsuario(id) {
    if (!confirm("⚠️ ¿Estás seguro de que deseas eliminar este usuario del sistema?")) return;

    try {
        const resp = await fetch(`${API_BASE}/Usuarios/${id}`, {
            method: 'DELETE',
            headers: getHeaders()
        });
        if (resp.ok) {
            alert("✅ Usuario eliminado exitosamente.");
            cargarTablaUsuarios();
        } else {
            alert("❌ Error al eliminar el usuario.");
        }
    } catch (e) { console.error("Error eliminando usuario:", e); }
}

// Adjuntamos las funciones al puente global de la ventana para que las etiquetas onclick del HTML las reconozcan
window.editarUsuario = editarUsuario;
window.eliminarUsuario = eliminarUsuario;