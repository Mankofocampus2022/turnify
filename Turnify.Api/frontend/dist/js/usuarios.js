/* =========================================
   TURNIFY - GESTIÓN DE USUARIOS (PRO)
   ========================================= */
const API_URL = 'http://localhost:5000/api/Usuarios';
let listaUsuariosGlobal = []; 

// 1. EL GUARDIÁN (Blindado)
document.addEventListener('DOMContentLoaded', () => {
    // 🛡️ Buscamos en ambas llaves para evitar el "bug loco" de redirección
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    if (!token || token === "undefined" || token === "null") {
        console.error("🚨 Acceso denegado: Token no encontrado.");
        window.location.href = 'login.html';
        return;
    }

    console.log("🔐 Sesión validada. Cargando sistema...");
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

// 2. OBTENER DATOS DE LA API (Con manejo de errores 401)
async function cargarUsuarios() {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    try {
        const response = await fetch(API_URL, {
            method: 'GET',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });

        // 🛡️ Si la API dice que el token no vale (401), limpiamos y salimos
        if (response.status === 401) {
            console.warn("⚠️ Token inválido o expirado.");
            logout();
            return;
        }

        if (response.ok) {
            listaUsuariosGlobal = await response.json(); 
            renderizarTabla(listaUsuariosGlobal); 
        } else {
            console.error("❌ Error API:", response.statusText);
        }
    } catch (error) {
        console.error("🚨 Error de conexión:", error);
    }
}

// 3. PINTAR LA TABLA (Normalización de datos Senior)
function renderizarTabla(usuarios) {
    const tabla = document.getElementById('tablaUsuarios');
    if (!tabla) return;

    const userRoleActual = (localStorage.getItem('usuario_rol') || "").toUpperCase(); 
    
    let htmlContent = ''; 

    usuarios.forEach(u => {
        const id = u.id || u.Id;
        const nombre = u.nombre || u.Nombre || "Sin nombre";
        const email = u.email || u.Email || "Sin email";
        const rol = u.rol || u.Rol || "Usuario"; 
        const bloqueado = u.esta_bloqueado ?? u.Esta_bloqueado ?? false;
        const fechaFinRaw = u.suscripcion_fin || u.Suscripcion_fin;
        const fechaFin = fechaFinRaw ? new Date(fechaFinRaw).toLocaleDateString() : 'N/A';

        const statusClass = bloqueado ? 'status-bloqueado' : 'status-activo';
        const statusText = bloqueado ? '🚫 Suspendido' : '✅ Activo';

        let botonesExtra = '';

        if (rol.toLowerCase().includes('barbero') || rol.toLowerCase().includes('proveedor')) {
            botonesExtra += `
                <button class="btn-action" style="background-color: #ffc107; color: #000;" onclick="gestionarTarjeta('${id}')">
                    <i class="fas fa-id-card"></i>
                </button>`;
        }

        if (userRoleActual.includes('ADMIN')) {
            botonesExtra += `
                <button class="btn-action" style="background-color: #48c1b5; color: #1b3d5f;" onclick="renovarSuscripcion('${id}')">
                    <i class="fas fa-calendar-plus"></i>
                </button>`;
        }

        htmlContent += `
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
                        <button class="btn-action ${bloqueado ? 'btn-activar' : 'btn-bloquear'}" onclick="toggleUser('${id}', ${bloqueado})">
                            <i class="fas ${bloqueado ? 'fa-check' : 'fa-ban'}"></i>
                        </button>
                        ${botonesExtra}
                    </div>
                </td>
            </tr>
        `;
    });

    tabla.innerHTML = htmlContent;
}

// 4. ACCIONES (Normalizadas)
async function toggleUser(id, estadoActual) {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
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
            cargarUsuarios(); 
        }
    } catch (error) { console.error(error); }
}

async function renovarSuscripcion(id) {
    const meses = prompt("¿Cuántos meses desea agregar?", "1");
    if (!meses) return;
    
    const numMeses = parseInt(meses);
    if (isNaN(numMeses) || numMeses <= 0) return alert("⚠️ Número inválido");

    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    try {
        const response = await fetch(`${API_URL}/renovar/${id}?meses=${numMeses}`, {
            method: 'PUT',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            alert("✅ Renovado con éxito");
            cargarUsuarios();
        }
    } catch (error) { console.error(error); }
}

// --- 🚩 5. FUNCIONALIDAD DE LA TARJETA DIGITAL (NUEVO) ---

function gestionarTarjeta(id) {
    // Buscamos los datos del usuario en la lista que ya cargamos de la API
    const usuario = listaUsuariosGlobal.find(u => (u.id || u.Id) === id);
    
    if (!usuario) {
        alert("❌ Error: No se encontró la información del usuario.");
        return;
    }

    const nombre = usuario.nombre || usuario.Nombre || "Barbero Profesional";
    const rol = usuario.rol || usuario.Rol || "Especialista";

    // Llenamos el modal
    document.getElementById('tarjetaNombre').innerText = nombre;
    document.getElementById('tarjetaRol').innerText = rol;
    
    // Limpiamos el QR anterior para que no se amontone
    const qrContainer = document.getElementById('qrcode');
    qrContainer.innerHTML = "";

    // Mostramos el modal
    document.getElementById('modalTarjeta').style.display = 'flex';

    // Generamos el código QR con el link de agendamiento
    // 🛡️ TIP: Aquí puedes poner el link real de tu frontend para clientes
    const linkReserva = `http://localhost:5000/agendar.html?barbero=${id}`;

    new QRCode(qrContainer, {
        text: linkReserva,
        width: 200,
        height: 200,
        colorDark : "#000000",
        colorLight : "#ffffff",
        correctLevel : QRCode.CorrectLevel.H
    });
}

function cerrarTarjeta() {
    const modal = document.getElementById('modalTarjeta');
    if (modal) modal.style.display = 'none';
}

function descargarQR() {
    const qrImg = document.querySelector('#qrcode img');
    if (!qrImg) return alert("❌ Primero genera el código QR.");

    const link = document.createElement('a');
    const nombreBarbero = document.getElementById('tarjetaNombre').innerText;
    
    link.href = qrImg.src;
    link.download = `QR_Turnify_${nombreBarbero.replace(/\s+/g, '_')}.png`;
    link.click();
}

// Cerrar modal al hacer clic afuera de la tarjeta
window.onclick = function(event) {
    const modal = document.getElementById('modalTarjeta');
    if (event.target === modal) {
        cerrarTarjeta();
    }
};

// 6. ✅ CIERRE DE SESIÓN LIMPIO
window.logout = function() {
    console.log("🧹 Limpiando sesión...");
    localStorage.clear(); 
    window.location.href = 'login.html'; 
};