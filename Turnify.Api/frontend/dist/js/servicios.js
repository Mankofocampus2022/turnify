const API_URL = 'http://localhost:5000/api/Servicios';

document.addEventListener('DOMContentLoaded', () => {
    // --- 🚩 ADICIÓN SENIOR: EL PUENTE DE DATOS (NO QUITA NADA, SOLO SINCRONIZA) ---
    if (!localStorage.getItem('turnify_token') && localStorage.getItem('token')) {
        localStorage.setItem('turnify_token', localStorage.getItem('token'));
    }
    if (!localStorage.getItem('proveedor_id') && localStorage.getItem('proveedorId')) {
        localStorage.setItem('proveedor_id', localStorage.getItem('proveedorId'));
    }
    // ----------------------------------------------------------------------------

    const proveedorId = localStorage.getItem('proveedor_id');
    
    if (!proveedorId || proveedorId === "null") {
        console.error("🚫 Sesión inválida en Servicios. Redirigiendo...");
        alert("Sesión inválida. Por favor, inicia sesión de nuevo.");
        window.location.href = 'login.html';
        return;
    }

    cargarServicios();
    
    const form = document.getElementById('formServicio');
    if(form) {
        form.addEventListener('submit', guardarServicio);
    }
});

// 1. CARGAR SERVICIOS
async function cargarServicios() {
    const token = localStorage.getItem('turnify_token');
    const proveedorId = localStorage.getItem('proveedor_id');
    const rol = localStorage.getItem('usuario_rol');

    // Normalizamos el rol a Mayúsculas para la comparación
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

    servicios.forEach(s => {
        const categoria = s.categoria || 'Barbería';
        const catClass = categoria === 'Manicura' ? 'cat-manicura' : 'cat-barberia';
        
        let estadoTexto = 'INACTIVO';
        let estadoClase = 'badge-danger';

        if (s.activo == 1) {
            estadoTexto = 'ACTIVO';
            estadoClase = 'badge-success';
        } else if (s.activo == 2) {
            estadoTexto = 'EN PROCESO';
            estadoClase = 'badge-warning';
        }

        const estadoBadge = `<span class="badge ${estadoClase}">${estadoTexto}</span>`;

        tabla.innerHTML += `
            <tr>
                <td>
                    <div style="font-weight: bold; font-size: 1.1em; color: white;">${s.nombre}</div>
                    <div style="color: #48c1b5; font-size: 0.85em;">ID: ${s.id.substring(0,8)}...</div>
                </td>
                <td><span class="role-pill ${catClass}">${categoria}</span></td>
                <td style="font-weight: 600; color: white;">$${s.precio.toLocaleString()}</td>
                <td style="color: #e2e8f0;"><i class="far fa-clock"></i> ${s.duracionMinutos} min</td>
                <td style="text-align: center;">${estadoBadge}</td>
                <td style="text-align: center;">
                    <div style="display: flex; justify-content: center; gap: 8px;">
                        <button class="btn-edit" onclick="editarServicio('${s.id}')">
                            <i class="fas fa-pen"></i> Editar
                        </button>
                        <button class="btn-action btn-bloquear" style="padding: 8px 12px;" onclick="eliminarServicio('${s.id}')">
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
            document.getElementById('nombreServicio').value = s.nombre;
            document.getElementById('precioServicio').value = s.precio;
            document.getElementById('duracionServicio').value = s.duracionMinutos;
            document.getElementById('comisionServicio').value = s.comisionPorcentaje;
            document.getElementById('estadoServicio').value = s.activo;
            
            document.getElementById('formServicio').setAttribute('data-id', s.id);
            
            abrirModal();
            const titulo = document.querySelector('.modal-header h2');
            if(titulo) titulo.innerHTML = '<i class="fas fa-edit"></i> Editar Servicio';
        }
    } catch (err) { 
        console.error("Error al cargar para editar:", err); 
    }
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
    const proveedorId = localStorage.getItem('proveedor_id');
    
    const form = document.getElementById('formServicio');
    const idExistente = form.getAttribute('data-id');

    const body = {
        nombre: document.getElementById('nombreServicio').value.trim(),
        categoria: "Barbería", 
        precio: parseFloat(document.getElementById('precioServicio').value) || 0,
        duracionMinutos: parseInt(document.getElementById('duracionServicio').value) || 0,
        proveedorId: proveedorId, 
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
            console.error("Error de la API:", errorData);
            alert("Error al guardar: " + (errorData.title || "Verifica los datos"));
        }
    } catch (error) { 
        console.error("Error de red:", error); 
    }
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
    localStorage.clear(); // Limpieza total senior
    alert("Cerrando sesión...");
    window.location.href = 'login.html';
}