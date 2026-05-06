/* =========================================
   TURNIFY - GESTIÓN DE SERVICIOS 
   ========================================= */

const API_URL = 'http://localhost:5000/api/Servicios';

document.addEventListener('DOMContentLoaded', () => {
    // 1. Sincronización de Identidad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    const userStr = localStorage.getItem('user'); // 🚩 Traemos el objeto completo de respaldo
    
    // 🛡️ BLINDAJE: Limpiamos el ID de posibles strings basura
    let pIdRaw = localStorage.getItem('proveedor_id') || localStorage.getItem('proveedorId');
    let proveedorId = (pIdRaw === "null" || pIdRaw === "undefined") ? null : pIdRaw;
    
    if (!token) {
        window.location.href = 'login.html';
        return;
    }

    // 🚩 RESCATE DE LUPE: Si el ID no está suelto, lo buscamos dentro del objeto user
    if (!proveedorId && userStr) {
        try {
            const userObj = JSON.parse(userStr);
            // 🛡️ CRÍTICO: El ID de la tabla 'usuarios' (userObj.id) NO es el mismo que el de 'proveedores'
            // Solo aceptamos proveedorId. Si no está, Maruja no podrá crear servicios.
            proveedorId = userObj.proveedorId || userObj.ProveedorId;
            
            console.log("🛠️ [Lupe Debug] ID de Proveedor rescatado:", proveedorId);
        } catch (e) { 
            console.error("❌ Error al parsear user en servicios"); 
        }
    }

    localStorage.setItem('turnify_token', token);
    
    // 🛡️ BLINDAJE: Aseguramos que el ID rescatado quede guardado para las peticiones
    if (proveedorId) {
        localStorage.setItem('proveedor_id', proveedorId);
    }

    // 2. VALIDACIÓN FLEXIBLE DE ROLES
    const rolNormalizado = rol.trim();
    const esAdmin = rolNormalizado.includes("ADMIN") || 
                    rolNormalizado.includes("6A7FA68F") || 
                    rolNormalizado.includes("6DE2A606") ||
                    rolNormalizado.includes("SUPERADMIN");

    const idValido = (proveedorId && proveedorId !== "null" && proveedorId !== "undefined");

    // 🚩 Solo redirigimos si NO es admin Y tampoco logramos encontrar un ID de proveedor válido
    if (!esAdmin && !idValido) {
        console.error("🚫 Profesional sin ID identificado. Redirigiendo...");
        alert("Tu perfil de profesional no está configurado correctamente. Por favor, inicia sesión de nuevo.");
        window.location.href = 'login.html';
        return;
    }

    console.log("✅ Acceso concedido como:", esAdmin ? "Admin" : "Profesional (" + rolNormalizado + ")");
    cargarServicios();
    
    const form = document.getElementById('formServicio');
    if(form) form.addEventListener('submit', guardarServicio);
});

// 1. CARGAR SERVICIOS (Lógica de filtrado mejorada)
async function cargarServicios() {
    const token = localStorage.getItem('turnify_token');
    const proveedorId = localStorage.getItem('proveedor_id');
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();

    const esAdmin = rol.includes("ADMIN") || rol.includes("SUPERADMIN") || rol.includes("6A7FA68F");
    
    // 🛡️ BLINDAJE: Si no hay proveedorId y no es admin, no hacemos la petición inútil
    if (!esAdmin && !proveedorId) return;

    const url = esAdmin ? API_URL : `${API_URL}/proveedor/${proveedorId}`;
    console.log("📡 Cargando servicios desde:", url);

    try {
        const response = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const datos = await response.json();
            if (Array.isArray(datos)) {
                // 🛡️ SEGUNDO FILTRO: Aseguramos que Maruja solo vea sus servicios
                const datosFiltrados = esAdmin ? datos : datos.filter(s => {
                    const sId = (s.proveedorId || s.ProveedorId || "").toString().toLowerCase();
                    const pId = (proveedorId || "").toString().toLowerCase();
                    return sId === pId;
                });
                renderizarTabla(datosFiltrados);
            }
        } else {
            console.error("❌ Error API Servicios:", response.status);
        }
    } catch (error) {
        console.error("Error de conexión:", error);
    }
}

// 2. RENDERIZAR TABLA (Manteniendo tu diseño original intacto)
function renderizarTabla(servicios) {
    const tabla = document.getElementById('tablaServicios');
    if(!tabla) return;
    
    tabla.innerHTML = '';
    if (!servicios || servicios.length === 0) {
        tabla.innerHTML = '<tr><td colspan="6" style="text-align: center; padding: 20px; color: #48c1b5;">No hay servicios registrados para este perfil.</td></tr>';
        return;
    }

    servicios.forEach(s => {
        const id = s.id || s.Id || '';
        const nombre = s.nombre || s.Nombre || 'Sin nombre';
        const precio = s.precio || s.Precio || 0;
        const duracionMinutos = s.duracionMinutos || s.DuracionMinutos || 0;
        const categoria = s.categoria || s.Categoria || 'Barbería';
        const activo = (s.activo !== undefined) ? s.activo : (s.Activo !== undefined ? s.Activo : true);
        const catClass = categoria.toLowerCase() === 'manicura' ? 'cat-manicura' : 'cat-barberia';
        
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

// 3. EDITAR SERVICIO (Mantenido intacto)
async function editarServicio(id) {
    const token = localStorage.getItem('turnify_token');
    try {
        const res = await fetch(`${API_URL}/${id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const s = await res.json();
            document.getElementById('nombreServicio').value = s.nombre || s.Nombre || '';
            document.getElementById('precioServicio').value = s.precio || s.Precio || 0;
            document.getElementById('duracionServicio').value = s.duracionMinutos || s.DuracionMinutos || 0;
            document.getElementById('comisionServicio').value = s.comisionPorcentaje || s.ComisionPorcentaje || 0;
            document.getElementById('estadoServicio').value = (s.activo !== undefined) ? s.activo : (s.Activo ? 1 : 0);
            document.getElementById('categoriaServicio').value = s.categoria || s.Categoria || 'Barbería';
            document.getElementById('formServicio').setAttribute('data-id', s.id || s.Id);
            abrirModal();
            const titulo = document.querySelector('.modal-header h2');
            if(titulo) titulo.innerHTML = '<i class="fas fa-edit"></i> Editar Servicio';
        }
    } catch (err) { console.error("Error al cargar para editar:", err); }
}

// 4. ELIMINAR SERVICIO (Mantenido intacto)
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

// 5. GUARDAR (CREAR O EDITAR) - 🛡️ BLINDAJE KILLER
async function guardarServicio(e) {
    e.preventDefault();
    const token = localStorage.getItem('turnify_token');
    let pId = localStorage.getItem('proveedor_id');
    
    // 🛡️ REGLA DE ORO: Si no hay proveedor_id, no enviamos nada
    if (!pId || pId === "null" || pId === "undefined") {
        alert("🚨 Error de sesión: No se encontró tu ID de proveedor. Por favor, cierra sesión y vuelve a entrar.");
        return;
    }

    const form = document.getElementById('formServicio');
    const idExistente = form.getAttribute('data-id');
    
    const body = {
        nombre: document.getElementById('nombreServicio').value.trim(),
        categoria: document.getElementById('categoriaServicio').value, 
        precio: parseFloat(document.getElementById('precioServicio').value) || 0,
        duracionMinutos: parseInt(document.getElementById('duracionServicio').value) || 0,
        proveedorId: pId, // 🚩 El ID correcto que rescatamos
        comisionPorcentaje: parseFloat(document.getElementById('comisionServicio').value) || 0,
        activo: parseInt(document.getElementById('estadoServicio').value) === 1, // Booleano para C#
        descripcion: "Servicio de Turnify" 
    };

    const metodo = idExistente ? 'PUT' : 'POST';
    const url = idExistente ? `${API_URL}/${idExistente}` : API_URL;

    console.log(`📡 Enviando ${metodo} a ${url}...`, body);

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
            console.error("❌ Error del API:", errorData);
            let msg = errorData.message || errorData.title || "Error en los datos";
            if(errorData.errors) msg = Object.values(errorData.errors).flat().join("\n");
            alert("Error al guardar:\n" + msg);
        }
    } catch (error) { 
        console.error("🚨 Error de red:", error); 
    }
}

// UTILIDADES (Sin cambios)
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
    window.location.href = 'login.html';
}

// PUENTE GLOBAL
window.abrirModal = abrirModal;
window.cerrarModal = cerrarModal;
window.editarServicio = editarServicio;
window.eliminarServicio = eliminarServicio;
window.logout = logout;