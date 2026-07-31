/* ============================================================
   TURNIFY - GESTIÓN DE SERVICIOS (PRO)
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el origen de red en caliente. Si corre localmente usa el puerto 5000 de .NET,
// si entran desde una IP local (ej: pruebas desde celulares) o dominio, reconfigura el host de inmediato para el catálogo de servicios.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

// 🛡️ BLINDAJE: URL Dinámica centralizada para evitar fallos en Docker/Producción
const API_URL = `${API_HOST}/api/Servicios`;

document.addEventListener('DOMContentLoaded', () => {
    // 1. Sincronización de Identidad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const rol = (localStorage.getItem('usuario_rol') || localStorage.getItem('user_role') || "").toUpperCase();
    const userStr = localStorage.getItem('user'); // 🚩 Traemos el objeto completo de respaldo
    
    // 🛡️ BLINDAJE: Limpiamos el ID de posibles strings basura
    let pIdRaw = localStorage.getItem('turnify_proveedor_id') || localStorage.getItem('proveedor_id') || localStorage.getItem('proveedorId');
    let proveedorId = (pIdRaw === "null" || pIdRaw === "undefined") ? null : pIdRaw;
    
    if (!token) {
        console.warn("⚠️ Sin sesión activa en Servicios. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 🚩 RESCATE DE LUPE: Si el ID no está suelto, lo buscamos dentro del objeto user
    if (!proveedorId && userStr) {
        try {
            const userObj = JSON.parse(userStr);
            // 🛡️ CRÍTICO: Mapeo de proveedorId desde el objeto de sesión
            proveedorId = userObj.proveedorId || userObj.ProveedorId || userObj.id;
            
            console.log("🛠️ [Lupe Debug] ID de Proveedor rescatado:", proveedorId);
        } catch (e) { 
            console.error("❌ Error al parsear user en servicios"); 
        }
    }

    localStorage.setItem('turnify_token', token);
    
    // 🛡️ BLINDAJE: Aseguramos que el ID rescatado quede guardado para las peticiones
    if (proveedorId) {
        localStorage.setItem('turnify_proveedor_id', proveedorId);
        localStorage.setItem('proveedor_id', proveedorId);
        localStorage.setItem('proveedorId', proveedorId);
    }

    // 2. VALIDACIÓN FLEXIBLE DE ROLES
    const rolNormalizado = rol.trim();
    const esAdmin = rolNormalizado.includes("ADMIN") || 
                    rolNormalizado.includes("STAFF") || 
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

    console.log("✅ Acceso concedido como:", esAdmin ? "Admin / Staff" : "Profesional (" + rolNormalizado + ")");
    cargarServicios();
    
    const form = document.getElementById('formServicio');
    if(form) form.addEventListener('submit', guardarServicio);
});

// 1. CARGAR SERVICIOS (Lógica de filtrado con Blindaje Senior)
async function cargarServicios() {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    let proveedorId = localStorage.getItem('turnify_proveedor_id') || localStorage.getItem('proveedor_id') || localStorage.getItem('proveedorId');
    const rol = (localStorage.getItem('usuario_rol') || localStorage.getItem('user_role') || "").toUpperCase().trim();

    const esAdmin = rol.includes("ADMIN") || rol.includes("STAFF") || rol.includes("SUPERADMIN") || rol.includes("6A7FA68F");
    
    // Fallback por si el proveedorId estaba guardado en user
    if (!proveedorId) {
        const userStr = localStorage.getItem('user');
        if (userStr) {
            try {
                const u = JSON.parse(userStr);
                proveedorId = u.proveedorId || u.ProveedorId || u.id;
            } catch (e) {}
        }
    }

    // 🛡️ BLINDAJE: Evitamos peticiones si no hay contexto de ID
    if (!esAdmin && !proveedorId) return;

    // 🚩 RUTA BLINDADA: Si es admin ve todo, si es proveedor ve solo su catálogo
    const url = esAdmin ? API_URL : `${API_URL}/proveedor/${proveedorId}`;
    console.log("📡 [Fetch] Servicios desde:", url);

    try {
        const response = await fetch(url, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const datos = await response.json();
            if (Array.isArray(datos)) {
                // 🛡️ SEGUNDO FILTRO: Seguridad Habeas Data nivel Front
                const datosFiltrados = esAdmin ? datos : datos.filter(s => {
                    const sId = (s.proveedorId || s.ProveedorId || "").toString().toLowerCase();
                    const pId = (proveedorId || "").toString().toLowerCase();
                    return sId === pId;
                });
                renderizarTabla(datosFiltrados);
            }
        } else {
            console.error("❌ Error API Servicios:", response.status);
            if(response.status === 401) window.location.href = 'login.html';
        }
    } catch (error) {
        console.error("🚨 Error de conexión en carga de servicios:", error);
    }
}

// 2. RENDERIZAR TABLA (Manteniendo tu diseño original Glass)
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
        
        // 🛡️ Estética Senior: Clases dinámicas por categoría
        const catClass = categoria.toLowerCase().includes('manicura') || categoria.toLowerCase().includes('pies') ? 'cat-manicura' : 'cat-barberia';
        
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
                <td style="font-weight: 600; color: white;">$${precio.toLocaleString('es-CO')}</td>
                <td style="color: #e2e8f0;"><i class="far fa-clock"></i> ${duracionMinutos} min</td>
                <td style="text-align: center;"><span class="badge ${estadoClase}">${estadoTexto}</span></td>
                <td style="text-align: center;">
                    <div style="display: flex; justify-content: center; gap: 8px;">
                        <button class="btn-edit" onclick="editarServicio('${id}')" title="Editar">
                            <i class="fas fa-pen"></i>
                        </button>
                        <button class="btn-action btn-bloquear" style="padding: 8px 12px;" onclick="eliminarServicio('${id}')" title="Eliminar">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    });
}

// 3. EDITAR SERVICIO (Blindado con Mapping Inverso)
async function editarServicio(id) {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    try {
        const res = await fetch(`${API_URL}/${id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const s = await res.json();
            // 🛡️ Mapeo robusto: Aceptamos mayúsculas o minúsculas del API
            document.getElementById('nombreServicio').value = s.nombre || s.Nombre || '';
            document.getElementById('precioServicio').value = s.precio || s.Precio || 0;
            document.getElementById('duracionServicio').value = s.duracionMinutos || s.DuracionMinutos || 0;
            document.getElementById('comisionServicio').value = s.comisionPorcentaje || s.ComisionPorcentaje || 0;
            
            // Lógica de estado (1: Activo, 0: Inactivo)
            const valActivo = (s.activo !== undefined) ? s.activo : s.Activo;
            document.getElementById('estadoServicio').value = (valActivo === true || valActivo == 1) ? 1 : 0;
            
            document.getElementById('categoriaServicio').value = s.categoria || s.Categoria || 'Barbería';
            document.getElementById('formServicio').setAttribute('data-id', s.id || s.Id);
            
            abrirModal();
            const titulo = document.querySelector('.modal-header h2');
            if(titulo) titulo.innerHTML = '<i class="fas fa-edit"></i> Editar Servicio';
        }
    } catch (err) { console.error("🚨 Error al cargar para editar:", err); }
}

// 4. ELIMINAR SERVICIO (Blindado)
async function eliminarServicio(id) {
    if (!confirm("⚠️ ¿Estás seguro de que quieres borrar este servicio? Esta acción no se puede deshacer.")) return;
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    try {
        const res = await fetch(`${API_URL}/${id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            alert("✅ Servicio eliminado correctamente.");
            cargarServicios();
        } else {
            alert("❌ No se pudo eliminar el servicio. Verifique si tiene citas asociadas.");
        }
    } catch (err) { console.error("🚨 Error al eliminar:", err); }
}

// 5. GUARDAR (CREAR O EDITAR) - 🛡️ BLINDAJE DE NEGOCIO
async function guardarServicio(e) {
    e.preventDefault();
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    let pId = localStorage.getItem('turnify_proveedor_id') || localStorage.getItem('proveedor_id') || localStorage.getItem('proveedorId');
    
    if (!pId) {
        const userStr = localStorage.getItem('user');
        if (userStr) {
            try {
                const u = JSON.parse(userStr);
                pId = u.proveedorId || u.ProveedorId || u.id;
            } catch (err) {}
        }
    }

    // 🛡️ REGLA DE ORO: Si no hay proveedor_id, el Backend rechazará la creación
    if (!pId || pId === "null" || pId === "undefined") {
        alert("🚨 Error de sesión crítica: No se encontró tu ID de proveedor. Por favor, reinicia sesión.");
        window.location.href = 'login.html';
        return;
    }

    const form = document.getElementById('formServicio');
    const idExisting = form.getAttribute('data-id');
    
    // 🚩 CONSTRUCCIÓN DEL DTO: Sincronizado con ServicioCreateDto de C#
    const body = {
        nombre: document.getElementById('nombreServicio').value.trim(),
        categoria: document.getElementById('categoriaServicio').value, 
        precio: parseFloat(document.getElementById('precioServicio').value) || 0,
        duracionMinutos: parseInt(document.getElementById('duracionServicio').value) || 0,
        proveedorId: pId, 
        comisionPorcentaje: parseFloat(document.getElementById('comisionServicio').value) || 0,
        activo: parseInt(document.getElementById('estadoServicio').value) === 1,
        descripcion: `Servicio de ${document.getElementById('categoriaServicio').value} actualizado desde el panel.` 
    };

    const metodo = idExisting ? 'PUT' : 'POST';
    const url = idExisting ? `${API_URL}/${idExisting}` : API_URL;

    // UI Feedback
    const btnSubmit = form.querySelector('button[type="submit"]');
    const originalText = btnSubmit.innerHTML;
    btnSubmit.disabled = true;
    btnSubmit.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando...';

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
            alert(idExisting ? "✨ ¡Servicio actualizado con éxito!" : "✨ ¡Nuevo servicio creado correctamente!");
            cerrarModal();
            cargarServicios();
        } else {
            // 🛡️ BLINDAJE ANTI-CRASH DE QA SENIOR: Validamos si la respuesta es JSON antes de parsear
            let errorData = { message: "Datos inválidos" };
            const contentType = res.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                errorData = await res.json();
            } else {
                const textFallback = await res.text();
                errorData.message = textFallback || errorData.message;
            }

            console.error("❌ Error del API:", errorData);
            let msg = errorData.message || errorData.title || "Datos inválidos";
            if(errorData.errors) msg = Object.values(errorData.errors).flat().join("\n");
            alert("Error al procesar el servicio:\n" + msg);
        }
    } catch (error) { 
        console.error("🚨 Error de red en guardarServicio:", error); 
        alert("🔌 Error de conexión con el servidor de Turnify.");
    } finally {
        btnSubmit.disabled = false;
        btnSubmit.innerHTML = originalText;
    }
}

// UTILIDADES (Sin cambios, manteniendo tu estructura)
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
    if(confirm("¿Seguro que quieres cerrar sesión?")){
        localStorage.clear();
        window.location.href = 'login.html';
    }
}

// PUENTE GLOBAL (Imprescindible para los onclick de la tabla)
window.abrirModal = abrirModal;
window.cerrarModal = cerrarModal;
window.editarServicio = editarServicio;
window.eliminarServicio = eliminarServicio;
window.logout = logout;