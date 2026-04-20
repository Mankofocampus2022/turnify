const API_URL = 'http://localhost:5000/api/Usuarios';
let listaUsuariosGlobal = []; 

// 1. INICIALIZACIÓN
document.addEventListener('DOMContentLoaded', () => {
    const token = localStorage.getItem('turnify_token');
    if (!token) {
        window.location.href = 'login.html';
        return;
    }

    cargarUsuarios();

    // 🔥 ESCUCHADOR DEL BUSCADOR (FILTRO LOCAL)
    const inputBusqueda = document.getElementById('inputBusqueda');
    if (inputBusqueda) {
        inputBusqueda.addEventListener('input', (e) => {
            const texto = e.target.value.toLowerCase();
            const filtrados = listaUsuariosGlobal.filter(u => {
                const nombre = (u.nombre || u.Nombre || "").toLowerCase();
                const email = (u.email || u.Email || "").toLowerCase();
                return nombre.includes(texto) || email.includes(texto);
            });
            renderizarTabla(filtrados);
        });
    }
});

// 2. OBTENER DATOS DE LA API
async function cargarUsuarios() {
    const token = localStorage.getItem('turnify_token');
    try {
        const response = await fetch(API_URL, {
            method: 'GET',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json'
            }
        });

        if (response.ok) {
            listaUsuariosGlobal = await response.json(); 
            renderizarTabla(listaUsuariosGlobal); 
        } else {
            console.error("Error al obtener usuarios:", response.statusText);
        }
    } catch (error) {
        console.error("Error de conexión:", error);
        alert("🚀 Error de conexión con la API en el puerto 5000");
    }
}

// 3. PINTAR LA TABLA (CON LÓGICA DE ROLES)
function renderizarTabla(usuarios) {
    const tabla = document.getElementById('tablaUsuarios');
    // 1. Normalizamos el rol actual a Mayúsculas
    const userRoleActual = (localStorage.getItem('usuario_rol') || "").toUpperCase(); 
    tabla.innerHTML = ''; 

    usuarios.forEach(u => {
        const nombre = u.nombre || u.Nombre || "Sin nombre";
        const email = u.email || u.Email || "Sin email";
        const rol = u.rol || u.Rol || "Sin Rol"; 
        const bloqueado = u.esta_bloqueado ?? u.Esta_bloqueado ?? false;
        const fechaFinRaw = u.suscripcion_fin || u.Suscripcion_fin;
        const fechaFin = fechaFinRaw ? new Date(fechaFinRaw).toLocaleDateString() : 'N/A';

        const statusClass = bloqueado ? 'status-bloqueado' : 'status-activo';
        const statusText = bloqueado ? '🚫 Suspendido' : '✅ Activo';

        let botonesExtra = '';

        // Botón para Barberos (ya lo tenías en minúsculas, está bien)
        if (rol.toLowerCase() === 'barbero') {
            botonesExtra += `
                <button class="btn-action" style="background-color: #ffc107; color: #000;" onclick="gestionarTarjeta('${u.id}')">
                    💳 Tarjeta
                </button>`;
        }

        // 2. CORRECCIÓN AQUÍ: Comparamos contra "SUPERADMIN" o "SUPERADMINISTRADOR"
        if (userRoleActual === 'SUPERADMIN' || userRoleActual === 'SUPERADMINISTRADOR') {
            botonesExtra += `
                <button class="btn-action" style="background-color: #48c1b5; color: #1b3d5f;" onclick="renovarSuscripcion('${u.id}')">
                    <i class="fas fa-calendar-plus"></i> Renovar
                </button>`;
        }

        tabla.innerHTML += `
            <tr>
                <td class="td-user"><strong>${nombre}</strong></td>
                <td class="td-user">${email}</td>
                <td class="td-user">
                    <span class="role-pill ${rol.toLowerCase()}">${rol}</span>
                </td>
                <td class="td-user">${fechaFin}</td>
                <td><span class="status-pill ${statusClass}">${statusText}</span></td>
                <td>
                    <div style="display: flex; gap: 8px;">
                        <button class="btn-action ${bloqueado ? 'btn-activar' : 'btn-bloquear'}" onclick="toggleUser('${u.id}', ${bloqueado})">
                            ${bloqueado ? 'Activar' : 'Bloquear'}
                        </button>
                        ${botonesExtra}
                    </div>
                </td>
            </tr>
        `;
    });
}

// 4. ACCIONES (BLOQUEAR, RENOVAR, TARJETA)
async function toggleUser(id, estadoActual) {
    const token = localStorage.getItem('turnify_token');
    const nuevoEstado = !estadoActual;
    const accion = nuevoEstado ? 'BLOQUEAR' : 'ACTIVAR';

    if (!confirm(`¿Estás seguro de que quieres ${accion} a este usuario?`)) return;

    try {
        const response = await fetch(`${API_URL}/cambiar-estado/${id}?bloquear=${nuevoEstado}`, {
            method: 'PUT',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            alert(`¡Usuario ${accion.toLowerCase()}ado con éxito! 🚀`);
            cargarUsuarios(); 
        }
    } catch (error) { console.error(error); }
}

async function renovarSuscripcion(id) {
    // Usamos un prompt simple para pedir los meses. 
    const meses = prompt("¿Cuántos meses desea agregar a la suscripción?", "1");
    
    if (meses === null) return; // El usuario canceló
    
    const numMeses = parseInt(meses);
    if (isNaN(numMeses) || numMeses <= 0) {
        alert("⚠️ Por favor, ingrese un número de meses válido (ej: 1, 3, 12).");
        return;
    }

    const token = localStorage.getItem('turnify_token');
    
    try {
        // Pasamos el parámetro 'meses' en la URL
        const response = await fetch(`${API_URL}/renovar/${id}?meses=${numMeses}`, {
            method: 'PUT',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const data = await response.json();
            const fecha = new Date(data.nuevaFecha).toLocaleDateString();
            alert(`✅ ¡Suscripción renovada con éxito hasta el ${fecha}! 🥂`);
            cargarUsuarios(); // Recargamos la tabla para ver la nueva fecha
        } else {
            alert("❌ Hubo un error al intentar renovar.");
        }
    } catch (error) { 
        console.error(error);
        alert("🚀 Error de conexión con el servidor."); 
    }
}

// 5. ✅ CIERRE DE SESIÓN GLOBAL
window.logout = function() {
    console.log("Cerrando sesión...");
    localStorage.clear(); 
    window.location.href = 'login.html'; 
};