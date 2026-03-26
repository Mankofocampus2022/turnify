const API_URL = 'http://localhost:5000/api/Servicios';

document.addEventListener('DOMContentLoaded', () => {
    const proveedorId = localStorage.getItem('proveedor_id');
    
    if (!proveedorId || proveedorId === "null") {
        alert("Sesión inválida. Por favor, inicia sesión de nuevo.");
        window.location.href = 'login.html';
        return;
    }

    cargarServicios();
    
    // Verificamos que el formulario exista antes de ponerle el evento
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

    const url = (rol && rol.toUpperCase().includes("ADMIN")) 
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
        // Asignamos categoría por defecto si viene nula
        const categoria = s.categoria || 'Barbería';
        const catClass = categoria === 'Manicura' ? 'cat-manicura' : 'cat-barberia';
        
        // 🚦 Los 3 estados que pediste
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
            // Llenamos los campos usando los IDs del nuevo HTML
            document.getElementById('nombreServicio').value = s.nombre;
            document.getElementById('precioServicio').value = s.precio;
            document.getElementById('duracionServicio').value = s.duracionMinutos;
            document.getElementById('comisionServicio').value = s.comisionPorcentaje;
            document.getElementById('estadoServicio').value = s.activo;
            
            // 🚩 Guardamos el ID en un atributo del form para saber que estamos editando
            document.getElementById('formServicio').setAttribute('data-id', s.id);
            
            abrirModal();
            // Cambiamos el texto del título si existe el elemento
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

// 5. GUARDAR (CREAR O EDITAR) - VERSIÓN CORREGIDA
async function guardarServicio(e) {
    e.preventDefault();
    const token = localStorage.getItem('turnify_token');
    
    // 🚩 EL PROBLEMA ESTÁ AQUÍ: Verifica que este ID exista en tu tabla SQL 'proveedores'
    const proveedorId = localStorage.getItem('proveedor_id');
    
    // Obtenemos el ID si estamos editando
    const form = document.getElementById('formServicio');
    const idExistente = form.getAttribute('data-id');

    // Construcción del objeto exactamente como lo espera C#
    const body = {
        nombre: document.getElementById('nombreServicio').value.trim(),
        categoria: "Barbería", 
        precio: parseFloat(document.getElementById('precioServicio').value) || 0,
        duracionMinutos: parseInt(document.getElementById('duracionServicio').value) || 0,
        proveedorId: proveedorId, // <--- Este GUID debe ser REAL en la DB
        comisionPorcentaje: parseFloat(document.getElementById('comisionServicio').value) || 0,
        activo: parseInt(document.getElementById('estadoServicio').value) || 0,
        descripcion: "" 
    };

    console.log("Datos enviados a la API:", body);

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
            // Esto te dirá exactamente qué campo está fallando
            console.error("Error de la API:", errorData);
            alert("Error al guardar: " + (errorData.title || "Verifica los datos"));
        }
    } catch (error) { 
        console.error("Error de red:", error); 
    }
}

// Función para cerrar sesión
function logout() {
    // Borramos los datos de sesión
    localStorage.removeItem('turnify_token');
    localStorage.removeItem('proveedor_id');
    localStorage.removeItem('usuario_rol');
    
    // Redirigimos al login
    alert("Cerrando sesión...");
    window.location.href = 'login.html';
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
        
        // Restaurar título original
        const titulo = document.querySelector('.modal-header h2');
        if(titulo) titulo.innerHTML = '<i class="fas fa-plus-circle"></i> Configurar Servicio';
    }
}

// Función para cerrar sesión
function logout() {
    // Borramos los datos de sesión
    localStorage.removeItem('turnify_token');
    localStorage.removeItem('proveedor_id');
    localStorage.removeItem('usuario_rol');
    
    // Redirigimos al login
    alert("Cerrando sesión...");
    window.location.href = 'login.html';
}