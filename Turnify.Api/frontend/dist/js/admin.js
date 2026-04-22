// 1. CONFIGURACIÓN CENTRALIZADA Y SEGURIDAD CRÍTICA
const API_BASE = 'http://localhost:5000/api';
const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";

// El "Portero": Si no hay token o no es el rol correcto, ¡fuera!
const token = localStorage.getItem('turnify_token');
const userRole = (localStorage.getItem('usuario_rol') || "").toUpperCase();

if (!token || userRole !== SUPER_ADMIN_GUID) {
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
    const nombre = localStorage.getItem('usuario_nombre') || "Usuario";
    
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
                <td class="p-3"><span class="px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-xs">${user.rol?.nombre || 'Sin Rol'}</span></td>
                <td class="p-3">
                    <button class="text-blue-600 hover:text-blue-900 mr-2" onclick="editarUsuario('${user.id}')">✏️</button>
                    <button class="text-red-600 hover:text-red-900" onclick="eliminarUsuario('${user.id}')">🗑️</button>
                </td>
            </tr>`).join('');
    } catch (error) {
        console.error("❌ Error al cargar usuarios:", error);
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