document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const proveedorId = localStorage.getItem('proveedorId') || localStorage.getItem('proveedor_id');

    if (!token || !proveedorId) {
        console.error("🚫 Sin sesión activa");
        window.location.href = 'login.html';
        return;
    }

    // 2. GESTIÓN DE TABS (Interactividad del Menú)
    const menuItems = document.querySelectorAll('.config-menu-item');
    const sections = document.querySelectorAll('.config-content');

    menuItems.forEach((item, index) => {
        item.addEventListener('click', () => {
            // Estética
            menuItems.forEach(i => i.classList.remove('active'));
            item.classList.add('active');

            // Lógica: Ocultar todas y mostrar la seleccionada
            sections.forEach(s => s.style.display = 'none');
            
            // Usamos una lógica más limpia para mostrar secciones
            const sectionIds = ['content-perfil', 'content-horarios', 'content-pagos', 'content-notificaciones'];
            const targetId = sectionIds[index];
            
            if(targetId) {
                document.getElementById(targetId).style.display = 'block';
                if(targetId === 'content-horarios') cargarHorarios();
            }
        });
    });

    // 3. Cargar datos iniciales
    cargarDatosConfig(proveedorId, token);

    // 4. Evento para guardar Perfil
    const formPerfil = document.getElementById('formConfigPerfil');
    if(formPerfil) {
        formPerfil.addEventListener('submit', (e) => guardarConfig(e, proveedorId, token));
    }
});

// --- FUNCIONES DE CARGA Y GUARDADO ---

async function cargarDatosConfig(proveedorId, token) {
    try {
        const response = await fetch(`http://localhost:5000/api/Proveedores/${proveedorId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const data = await response.json();
            console.log("🏢 Datos del negocio cargados:", data);
            
            // Mapeo con fallback por si los nombres vienen en minúscula desde el JSON
            document.getElementById('negocioNombre').value = data.nombreComercial || data.nombre || '';
            document.getElementById('negocioEmail').value = data.email || '';
            document.getElementById('negocioTelefono').value = data.telefono || '';
            document.getElementById('negocioDireccion').value = data.direccion || '';
        }
    } catch (error) {
        console.error("🔥 Error al cargar configuración:", error);
    }
}

async function guardarConfig(e, proveedorId, token) {
    e.preventDefault();
    
    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando...';

    // 5. El BODY exacto para el DTO de C#
    const body = {
        Id: proveedorId, // "I" Mayúscula para el Guid
        NombreComercial: document.getElementById('negocioNombre').value.trim(),
        Direccion: document.getElementById('negocioDireccion').value.trim(),
        Tipo: "Barbería" 
    };

    try {
        const url = `http://localhost:5000/api/Proveedores/${proveedorId}`;

        const response = await fetch(url, {
            method: 'PUT', 
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify(body)
        });

        if (response.ok) {
            alert("✅ ¡Perfil actualizado con éxito, mi perro!");
            // Sincronizamos el nombre para el Dashboard
            localStorage.setItem('adminName', body.NombreComercial);
        } else {
            const err = await response.json();
            alert("❌ Error: " + (err.message || "No se pudo actualizar"));
        }
    } catch (error) {
        console.error("🔥 Error de red:", error);
        alert("🚀 Servidor no disponible o error de red.");
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalHTML;
    }
}

function cargarHorarios() {
    console.log("🕒 Próximamente: Carga de horarios desde la API");
}