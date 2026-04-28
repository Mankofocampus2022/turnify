/* =========================================
   TURNIFY - GESTIÓN DE SERVICIOS (PRO)
   ========================================= */

const API_URL = 'http://localhost:5000/api/Servicios';

document.addEventListener('DOMContentLoaded', () => {
    // 1. Sincronización de Identidad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const rol = localStorage.getItem('usuario_rol') || "";
    const proveedorId = localStorage.getItem('proveedor_id') || localStorage.getItem('proveedorId');
    
    if (!token) {
        window.location.href = 'login.html';
        return;
    }

    localStorage.setItem('turnify_token', token);
    
    // 🛡️ BLINDAJE: Solo guardamos el ID si es un valor real para evitar el string "null"
    if (proveedorId && proveedorId !== "null" && proveedorId !== "undefined") {
        localStorage.setItem('proveedor_id', proveedorId);
    }

    // 2. VALIDACIÓN FLEXIBLE DE ROLES
    const rolNormalizado = rol.toUpperCase();
    const esAdmin = rolNormalizado.includes("ADMIN") || 
                    rolNormalizado.includes("6A7FA68F") || 
                    rolNormalizado.includes("6DE2A606");

    const idValido = (proveedorId && proveedorId !== "null" && proveedorId !== "undefined");

    if (!esAdmin && !idValido) {
        console.error("🚫 Barbero sin ID de local. Redirigiendo...");
        alert("Tu perfil de barbero no está configurado. Por favor, inicia sesión.");
        window.location.href = 'login.html';
        return;
    }

    console.log("✅ Acceso concedido como:", esAdmin ? "Admin" : "Barbero");
    cargarServicios();
    
    const form = document.getElementById('formServicio');
    if(form) form.addEventListener('submit', guardarServicio);
});

// 1. CARGAR SERVICIOS
async function cargarServicios() {
    const token = localStorage.getItem('turnify_token');
    const proveedorId = localStorage.getItem('proveedor_id');
    const rol = localStorage.getItem('usuario_rol');
    const rolNormalizado = (rol || "").toUpperCase();

    const url = (rolNormalizado.includes("ADMIN") || rolNormalizado.includes("6A7FA68F") || rolNormalizado.includes("6DE2A606")) 
                ? API_URL 
                : `${API_URL}/proveedor/${proveedorId}`;

    try {
        const response = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        const datos = await response.json();
        if (response.ok && Array.isArray(datos)) {
            renderizarTabla(datos);
        }
    } catch (error) {
        console.error("Error de conexión:", error);
    }
}

// 2. RENDERIZAR TABLA
function renderizarTabla(servicios) {
    const tabla = document.getElementById('tablaServicios');
    if(!tabla) return;
    
    tabla.innerHTML = '';

    if (servicios.length === 0) {
        tabla.innerHTML = '<tr><td colspan="6" style="text-align: center; padding: 20px; color: #48c1b5;">No hay servicios registrados.</td></tr>';
        return;
    }

    servicios.forEach(s => {
        const id = s.id || s.Id || '';
        const nombre = s.nombre || s.Nombre || 'Sin nombre';
        const precio = s.precio || s.Precio || 0;
        const duracionMinutos = s.duracionMinutos || s.DuracionMinutos || 0;
        const categoria = s.categoria || s.Categoria || 'Barbería';
        const activo = (s.activo !== undefined) ? s.activo : s.Activo;
        
        const catClass = categoria === 'Manicura' ? 'cat-manicura' : 'cat-barberia';
        
        let estadoTexto = (activo == 1 || activo === true) ? 'ACTIVO' : (activo == 2 ? 'EN PROCESO' : 'INACTIVO');
        let estadoClase = (activo == 1 || activo === true) ? 'badge-success' : (activo == 2 ? 'badge-warning' : 'badge-danger');

        const idCorto = id ? id.toString().substring(0,8) : '...';

        tabla.innerHTML += `
            <tr>
                <td>
                    <div style="font-weight: bold; font-size: 1.1em; color: white;">${nombre}</div>
                    <div style="color: #48c1b5; font-size: 0.85em;">ID: ${idCorto}...</div>
                </td>
                <td><span class="role-pill ${catClass}">${categoria}</span></td>
                <td style="font-weight: 600; color: white;">$${precio.toLocaleString()}</td>
                <td style="color: #e2e8f0;"><i class="far fa-clock"></i> ${duracionMinutos} min</td>
                <td style="text-align: center;"><span class="badge ${estadoClase}">${estadoTexto}</span></td>
                <td style="text-align: center;">
                    <div style="display: flex; justify-content: center; gap: 8px;">
                        <button class="btn-edit" onclick="editarServicio('${id}')">
                            <i class="fas fa-pen"></i>
                        </button>
                        <button class="btn-action btn-bloquear" style="padding: 8px 12px;" onclick="eliminarServicio('${id}')">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    });
}

// 3. EDITAR SERVICIO
async function editarServicio(id) {
    const token = localStorage.getItem('turnify_token');
    try {
        const res = await fetch(`${API_URL}/${id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        const s = await res.json();

        if (res.ok) {
            document.getElementById('nombreServicio').value = s.nombre || s.Nombre || '';
            document.getElementById('precioServicio').value = s.precio || s.Precio || 0;
            document.getElementById('duracionServicio').value = s.duracionMinutos || s.DuracionMinutos || 0;
            document.getElementById('comisionServicio').value = s.comisionPorcentaje || s.ComisionPorcentaje || 0;
            document.getElementById('estadoServicio').value = (s.activo !== undefined) ? s.activo : (s.Activo ? 1 : 0);
            
            document.getElementById('formServicio').setAttribute('data-id', s.id || s.Id);
            
            abrirModal();
            const titulo = document.querySelector('.modal-header h2');
            if(titulo) titulo.innerHTML = '<i class="fas fa-edit"></i> Editar Servicio';
        }
    } catch (err) { console.error("Error al cargar para editar:", err); }
}

// 4. ELIMINAR SERVICIO
async function eliminarServicio(id) {
    if (!confirm("¿Seguro que quieres borrar este servicio?")) return;
    const token = localStorage.getItem('turnify_token');
    try {
        const res = await fetch(`${API_URL}/${id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) cargarServicios();
    } catch (err) { console.error("Error al eliminar:", err); }
}

// 5. GUARDAR (CREAR O EDITAR)
async function guardarServicio(e) {
    e.preventDefault();
    const token = localStorage.getItem('turnify_token');
    
    // 🛡️ BLINDAJE: Capturamos el proveedorId
    let pId = localStorage.getItem('proveedor_id');

    // Si es un texto "null" o está vacío, lo convertimos a un null real de JS para el API
    if (!pId || pId === "null" || pId === "undefined" || pId === "") {
        pId = null;
    }

    const form = document.getElementById('formServicio');
    const idExistente = form.getAttribute('data-id');

    // 🛡️ Construcción segura del objeto (Ahora el DTO sí aceptará este null)
    const body = {
        nombre: document.getElementById('nombreServicio').value.trim(),
        categoria: document.getElementById('categoriaServicio').value, 
        precio: parseFloat(document.getElementById('precioServicio').value) || 0,
        duracionMinutos: parseInt(document.getElementById('duracionServicio').value) || 0,
        proveedorId: pId, 
        comisionPorcentaje: parseFloat(document.getElementById('comisionServicio').value) || 0,
        activo: parseInt(document.getElementById('estadoServicio').value) || 0,
        descripcion: "" 
    };

    const metodo = idExistente ? 'PUT' : 'POST';
    const url = idExistente ? `${API_URL}/${idExistente}` : API_URL;

    try {
        const res = await fetch(url, {
            method: metodo,
            headers: { 
                'Content-Type': 'application/json', 
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify(body)
        });

        if (res.ok) {
            alert(idExistente ? "¡Servicio actualizado!" : "¡Servicio creado!");
            cerrarModal();
            cargarServicios();
        } else {
            const errorData = await res.json();
            let msg = errorData.title || "Error en los datos";
            if(errorData.errors) {
                msg = Object.values(errorData.errors).flat().join("\n");
            }
            alert("Error al guardar:\n" + msg);
        }
    } catch (error) { console.error("Error de red:", error); }
}

// UTILIDADES
function abrirModal() { 
    const modal = document.getElementById('modalServicio');
    if(modal) modal.style.display = 'flex'; 
}

function cerrarModal() { 
    const modal = document.getElementById('modalServicio');
    if(modal) {
        modal.style.display = 'none';
        document.getElementById('formServicio').reset();
        document.getElementById('formServicio').removeAttribute('data-id');
        const titulo = document.querySelector('.modal-header h2');
        if(titulo) titulo.innerHTML = '<i class="fas fa-plus-circle"></i> Configurar Servicio';
    }
}

function logout() {
    localStorage.clear();
    alert("Cerrando sesión...");
    window.location.href = 'login.html';
}

// 🚩 PUENTE GLOBAL: Esto es lo que soluciona el "ReferenceError"
window.abrirModal = abrirModal;
window.cerrarModal = cerrarModal;
window.editarServicio = editarServicio;
window.eliminarServicio = eliminarServicio;
window.logout = logout;