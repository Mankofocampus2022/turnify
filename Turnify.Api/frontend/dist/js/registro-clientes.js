/* ============================================================
   TURNIFY - MOTOR DE REGISTRO INTELIGENTE 
   ============================================================ */

// 1. CONFIGURACIÓN DE ROLES (GUIDs de tu base de datos SQL Server)
const ROLES = {
    CLIENTE: "56992f75-6420-4d55-a5f9-9223248c50d7",
    BARBERO: "8854c07c-6e5e-4876-a29a-c7ad5dcfbab7" // Rol de Proveedor/Admin
};

let currentRole = 'CLIENTE';

// 🚩 DETECCIÓN DE QR: Obtenemos el ID del proveedor desde la URL
const urlParams = new URLSearchParams(window.location.search);
const qrProveedorId = urlParams.get('id'); // Si viene de un QR, este ID existirá

/**
 * Función para alternar entre Cliente y Barbero en la UI
 */
function cambiarRol(rol) {
    currentRole = rol;
    
    const groupNegocio = document.getElementById('groupNegocio');
    const inputNegocio = document.getElementById('regNegocio');
    const btnText = document.getElementById('btnText');
    const btnCliente = document.getElementById('btnSoyCliente');
    const btnBarbero = document.getElementById('btnSoyBarbero');
    // 🚩 Si es barbero, ocultamos la sección de reserva inmediata (lógica de negocio)
    const sectionReserva = document.getElementById('sectionReservaInmediata');

    if (rol === 'BARBERO') {
        if(groupNegocio) groupNegocio.style.display = 'block';
        if(sectionReserva) sectionReserva.style.display = 'none';
        if(inputNegocio) inputNegocio.required = true;
        btnText.innerText = "Registrarme como Barbero";
        btnBarbero.classList.add('active');
        btnCliente.classList.remove('active');
    } else {
        if(groupNegocio) groupNegocio.style.display = 'none';
        // Si hay un QR detectado, volvemos a mostrar la reserva al ser cliente
        if(qrProveedorId && sectionReserva) sectionReserva.style.display = 'block';
        if(inputNegocio) inputNegocio.required = false;
        btnText.innerText = "Registrarme como Cliente";
        btnCliente.classList.add('active');
        btnBarbero.classList.remove('active');
    }
}

/**
 * 🚩 NUEVO: Lógica de Domicilio (Ocultar/Mostrar dirección)
 */
function toggleDireccionRegistro() {
    const modalidad = document.getElementById('regModalidad').value;
    const groupDireccion = document.getElementById('groupDireccionRegistro');
    const inputDireccion = document.getElementById('regDireccion');
    
    if (modalidad === 'domicilio') {
        groupDireccion.style.display = 'block';
        inputDireccion.required = true;
    } else {
        groupDireccion.style.display = 'none';
        inputDireccion.required = false;
    }
}

/**
 * 🚩 NUEVO: Cargar Servicios del Proveedor si viene por QR
 */
async function inicializarFlujoQR() {
    if (!qrProveedorId) return;

    // Mostramos la sección del "Boss"
    const sectionReserva = document.getElementById('sectionReservaInmediata');
    const divNegocio = document.getElementById('negocioDetected');
    const txtNegocio = document.getElementById('nombreNegocioQR');
    
    if(sectionReserva) sectionReserva.style.display = 'block';
    if(divNegocio) divNegocio.style.display = 'block';

    try {
        // Obtenemos los servicios de este barbero específico
        const response = await fetch(`http://localhost:5000/api/Servicios/proveedor/${qrProveedorId}`);
        if (response.ok) {
            const servicios = await response.json();
            const selectServicio = document.getElementById('regServicio');
            if(selectServicio) {
                selectServicio.innerHTML = servicios.map(s => `<option value="${s.id}">${s.nombre} - $${s.precio}</option>`).join('');
            }
            
            // También intentamos traer el nombre del negocio para el banner
            const respProv = await fetch(`http://localhost:5000/api/Proveedores/${qrProveedorId}`);
            if(respProv.ok) {
                const prov = await respProv.json();
                if(txtNegocio) txtNegocio.innerText = `Agendando en: ${prov.nombreComercial || prov.nombre}`;
            }
        }
    } catch (e) { console.error("Error cargando info de QR", e); }
}

// Escuchamos el cambio de fecha para cargar horas disponibles
document.getElementById('regFecha')?.addEventListener('change', async (e) => {
    const fecha = e.target.value;
    const servicioId = document.getElementById('regServicio').value;
    const selectHora = document.getElementById('regHora');

    if (!fecha || !servicioId) return;

    try {
        const response = await fetch(`http://localhost:5000/api/Citas/disponibilidad?proveedorId=${qrProveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        if (response.ok) {
            const horas = await response.json();
            selectHora.innerHTML = horas.map(h => `<option value="${h}">${h.slice(0, 5)}</option>`).join('');
        }
    } catch (e) { console.error("Error disponibilidad", e); }
});

/**
 * Manejador principal del Registro (CON SOPORTE DE RESERVA)
 */
document.getElementById('formRegistroCliente').addEventListener('submit', async (e) => {
    e.preventDefault();

    const btnSubmit = document.getElementById('btnSubmit');
    const password = document.getElementById('regPassword').value;
    const confirm = document.getElementById('regConfirmPassword').value;

    if (password !== confirm) {
        alert("⚠️ Las contraseñas no coinciden. Por favor, verifica.");
        return;
    }

    btnSubmit.disabled = true;
    btnSubmit.innerText = "Procesando Registro...";

    // 📦 1. DATOS DE USUARIO - 🛡️ BLINDAJE PARA EVITAR ERROR 400
    const registroData = {
        nombre: document.getElementById('regNombre').value.trim(),
        email: document.getElementById('regEmail').value.trim(),
        password: password,
        rol_id: currentRole === 'CLIENTE' ? ROLES.CLIENTE : ROLES.BARBERO,
        telefono: document.getElementById('regTelefono').value.trim(),
        nombreComercial: currentRole === 'BARBERO' ? document.getElementById('regNegocio').value.trim() : "",
        tipoNegocio: currentRole === 'BARBERO' ? (document.getElementById('regTipoNegocio')?.value || "Barbería") : "Particular"
    };

    try {
        // PASO A: REGISTRAR EL USUARIO
        const response = await fetch('http://localhost:5000/api/Usuarios/registrar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(registroData)
        });

        const result = await response.json();

        if (response.ok) {
            // 🚩 CORRECCIÓN CRÍTICA: El backend devuelve 'usuarioId' directamente en el objeto result
            const clienteId = result.usuarioId || result.id;

            // PASO B: SI ES FLUJO QR, AGENDAMOS DE UNA VEZ
            if (qrProveedorId && currentRole === 'CLIENTE' && clienteId) {
                btnSubmit.innerText = "Agendando tu cita...";
                
                const citaData = {
                    clienteId: clienteId,
                    proveedorId: qrProveedorId,
                    servicioId: document.getElementById('regServicio').value,
                    fecha: document.getElementById('regFecha').value,
                    hora: document.getElementById('regHora').value,
                    modalidad: document.getElementById('regModalidad').value,
                    direccion: document.getElementById('regDireccion').value.trim(),
                    metodoRegistro: "QR" 
                };

                const resCita = await fetch('http://localhost:5000/api/Citas/agendar', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(citaData)
                });

                if (resCita.ok) {
                    alert("🚀 ¡Cuenta creada y Cita Agendada! Te esperamos.");
                } else {
                    alert("✅ Cuenta creada, pero hubo un error agendando. Inicia sesión para intentarlo de nuevo.");
                }
            } else {
                alert("🚀 ¡Bienvenido a Turnify! Tu cuenta ha sido creada.");
            }
            
            window.location.href = 'login.html';
        } else {
            // 🛡️ CAPTURA DE ERRORES DE VALIDACIÓN (Si el backend manda un 400 con detalles)
            let errorMsg = result.message || "No se pudo completar el registro.";
            if(result.errors) {
                errorMsg = Object.values(result.errors).flat().join("\n");
            }
            alert("❌ Error: " + errorMsg);
            
            btnSubmit.disabled = false;
            btnSubmit.innerText = currentRole === 'CLIENTE' ? "Registrarme como Cliente" : "Registrarme como Barbero";
        }

    } catch (error) {
        console.error("🚨 Error:", error);
        alert("🔌 Error de conexión.");
        btnSubmit.disabled = false;
        btnSubmit.innerText = "Reintentar Registro";
    }
});

// Inicializamos si hay QR presente al cargar la página
if(qrProveedorId) inicializarFlujoQR();