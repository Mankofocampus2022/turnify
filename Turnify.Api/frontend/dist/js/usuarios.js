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
    const userRoleActual = localStorage.getItem('usuario_rol'); 
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

        // Botón para Barberos
        if (rol.toLowerCase() === 'barbero') {
            botonesExtra += `
                <button class="btn-action" style="background-color: #ffc107; color: #000;" onclick="gestionarTarjeta('${u.id}')">
                    💳 Tarjeta
                </button>`;
        }

        // Botón para SuperAdmin
        if (userRoleActual === 'SuperAdmin') {
            botonesExtra += `
                <button class="btn-action" style="background-color: #48c1b5; color: #1b3d5f;" onclick="renovarSuscripcion('${u.id}')">
                    +30 Días
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
    const token = localStorage.getItem('turnify_token');
    if (!confirm("¿Quieres extender la suscripción por 30 días?")) return;

    try {
        const response = await fetch(`${API_URL}/renovar/${id}`, {
            method: 'PUT',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            alert("¡Suscripción renovada! 🥂");
            cargarUsuarios();
        }
    } catch (error) { alert("Error al renovar."); }
}

function gestionarTarjeta(id) {
    alert("Función para gestionar tarjeta del barbero ID: " + id);
}

// 5. ✅ CIERRE DE SESIÓN GLOBAL
window.logout = function() {
    console.log("Cerrando sesión...");
    localStorage.clear(); 
    window.location.href = 'login.html'; 
};