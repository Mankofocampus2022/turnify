document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad (Versión Blindada)
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    let proveedorId = localStorage.getItem('proveedorId') || localStorage.getItem('proveedor_id');

    // 🚩 VALIDACIÓN SENIOR: Evitamos que las cadenas "null" o "undefined" pasen como IDs
    if (proveedorId === "null" || proveedorId === "undefined" || !proveedorId) {
        proveedorId = null;
    }

    // Si no hay token o el ID no parece un GUID (mínimo 30 caracteres), mandamos al login
    if (!token || !proveedorId || proveedorId.length < 30) {
        console.error("🚫 Sesión inválida o ID corrupto. Limpiando...");
        localStorage.clear(); // Limpiamos todo para entrar limpio la próxima vez
        window.location.href = 'login.html';
        return;
    }

    // 🚩 LOG DE DEPURACIÓN: Si ves este ID en la consola, es que vamos por buen camino
    console.log("✅ Sesión activa para el proveedor:", proveedorId);

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
                // 🚩 MEJORA: Ahora cargarHorarios es asíncrona para traer datos reales
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
            
            // 🚩 AGREGADO: Cargamos el Tipo de negocio (Barbería/Manicura) si el select existe
            if(document.getElementById('negocioTipo')) {
                document.getElementById('negocioTipo').value = data.tipo || 'Barbería';
            }
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
        // 🚩 CORRECCIÓN: Leemos el valor del select para que no sea solo "Barbería" fijo
        Tipo: document.getElementById('negocioTipo') ? document.getElementById('negocioTipo').value : "Barbería" 
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

async function cargarHorarios() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const dias = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];
    const contenedor = document.getElementById('lista-horarios');
    
    try {
        // 🚩 MEJORA CRÍTICA: Primero le preguntamos a la API qué horarios ya tiene guardados
        const response = await fetch('http://localhost:5000/api/Horarios/mi-semana', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        let horariosGuardados = [];
        if (response.ok) {
            horariosGuardados = await response.json();
        }

        // Renderizamos la interfaz de los 7 días con los datos reales
        contenedor.innerHTML = dias.map((dia, i) => {
            // Buscamos si existe un registro para este día (0-6)
            const h = horariosGuardados.find(x => x.diaSemana === i);
            
            // Si existe, usamos sus horas. Si no, ponemos las de defecto.
            const open = h ? h.horaApertura.slice(0, 5) : "08:00";
            const close = h ? h.horaCierre.slice(0, 5) : "20:00";
            const isClosed = h && h.horaApertura === "00:00:00" && h.horaCierre === "00:00:00";

            return `
                <div class="horario-row" style="display: flex; gap: 15px; margin-bottom: 15px; align-items: center; background: #122940; padding: 12px; border-radius: 10px; border: 1px solid rgba(72,193,181,0.2);">
                    <div style="width: 100px; color: #48c1b5;"><strong>${dia}</strong></div>
                    <input type="time" id="open-${i}" value="${open}" class="form-input" style="background: #1b3d5f; color: white; border: none; border-radius: 5px; padding: 5px;">
                    <span style="color: white;">a</span>
                    <input type="time" id="close-${i}" value="${close}" class="form-input" style="background: #1b3d5f; color: white; border: none; border-radius: 5px; padding: 5px;">
                    <label style="color: #e94560; cursor: pointer;">
                        <input type="checkbox" id="closed-${i}" ${isClosed ? 'checked' : ''}> Cerrado
                    </label>
                </div>
            `;
        }).join('');

        // Agregamos el evento al botón de guardar horarios (que está en el HTML)
        const btnSaveH = document.querySelector('#content-horarios .btn-save');
        if(btnSaveH) btnSaveH.onclick = guardarTodosLosHorarios;

    } catch (error) {
        console.error("🔥 Error al cargar horarios de la API:", error);
    }
}

async function guardarTodosLosHorarios() {
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const horarios = [];

    for (let i = 0; i < 7; i++) {
        const estaCerrado = document.getElementById(`closed-${i}`).checked;
        horarios.push({
            DiaSemana: i,
            HoraApertura: estaCerrado ? "00:00" : document.getElementById(`open-${i}`).value,
            HoraCierre: estaCerrado ? "00:00" : document.getElementById(`close-${i}`).value
        });
    }

    try {
        const response = await fetch('http://localhost:5000/api/Horarios/configurar-semana', {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify(horarios)
        });

        if (response.ok) {
            alert("✅ ¡Horarios sincronizados con éxito, jefe!");
            cargarHorarios(); // Refrescamos para confirmar los cambios
        } else {
            alert("❌ Error al guardar horarios.");
        }
    } catch (error) { console.error(error); }
}

// --- SECCIONES PENDIENTES DE INTEGRACIÓN ---

/*
// 💰 FUNCIÓN DE PAGOS (Para conectar con pasarela bancaria)
async function cargarDatosPagos(proveedorId, token) {
    console.log("💳 Iniciando conexión con el servicio de pagos...");
    // Aquí iría el fetch a un controlador de Pagos que maneje las API Keys
    // const res = await fetch(`http://localhost:5000/api/Configuracion/pagos/${proveedorId}`);
}

async function guardarConfigPagos() {
    // Aquí se enviaría la Public Key y la Pasarela seleccionada al servidor
    // Es vital que el servidor encripte estas llaves antes de guardarlas.
    console.log("🔒 Guardando credenciales bancarias de forma segura...");
}

// 📩 FUNCIÓN DE MENSAJERÍA (Para colas de RabbitMQ / Azure Bus)
async function cargarConfigNotificaciones(proveedorId, token) {
    console.log("🔔 Cargando preferencias de notificaciones...");
    // Aquí se cargarían los booleanos de si quiere Email, SMS o WhatsApp
}

async function guardarConfigNotificaciones() {
    // Al guardar aquí, el backend debería configurar los 'Topics' en el bus de mensajes
    console.log("🚀 Sincronizando cola de mensajes para recordatorios...");
}
*/