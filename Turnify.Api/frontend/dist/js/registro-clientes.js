/* ============================================================
   TURNIFY - MOTOR DE REGISTRO INTELIGENTE (MULTI-ROL + HU-10)
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el host en caliente.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

// 1. CONFIGURACIÓN DE ROLES (GUIDs y Claves de Sistema)
const ROLES = {
    CLIENTE: "56992f75-6420-4d55-a5f9-9223248c50d7",
    ADMINISTRADOR: "8854c07c-6e5e-4876-a29a-c7ad5dcfbab7", // Rol de Proveedor / Administrador del negocio
    INDEPENDIENTE: "PROVEEDOR_INDEPENDIENTE"
};

let currentRole = 'CLIENTE';

// 🚩 DETECCIÓN DE QR: Obtenemos el ID del proveedor desde la URL
const urlParams = new URLSearchParams(window.location.search);
const qrProveedorId = urlParams.get('id'); // Si viene de un QR, este ID existirá

/**
 * Función para alternar entre Cliente, Administrador e Independiente en la UI (HU-10)
 */
function cambiarRol(rol) {
    currentRole = rol;
    
    const groupNegocio = document.getElementById('groupNegocio');
    const groupEspecialidad = document.getElementById('groupEspecialidad');
    const groupFotoRostro = document.getElementById('groupFotoRostro');
    const groupDescripcion = document.getElementById('groupDescripcion');
    const inputNegocio = document.getElementById('regNegocio');
    const btnText = document.getElementById('btnText');
    const btnCliente = document.getElementById('btnSoyCliente');
    const btnBarbero = document.getElementById('btnSoyBarbero');
    const btnIndependiente = document.getElementById('btnSoyIndependiente');
    const sectionReserva = document.getElementById('sectionReservaInmediata');

    // Resetear estados activos de los botones
    if(btnCliente) btnCliente.classList.remove('active');
    if(btnBarbero) btnBarbero.classList.remove('active');
    if(btnIndependiente) btnIndependiente.classList.remove('active');

    if (rol === 'INDEPENDIENTE') {
        currentRole = 'INDEPENDIENTE';
        if(groupNegocio) groupNegocio.style.display = 'none';
        if(groupEspecialidad) groupEspecialidad.style.display = 'block';
        if(groupFotoRostro) groupFotoRostro.style.display = 'block';
        if(groupDescripcion) groupDescripcion.style.display = 'block';
        if(sectionReserva) sectionReserva.style.display = 'none';
        if(inputNegocio) inputNegocio.required = false;
        
        btnText.innerText = "Registrarme como Independiente";
        if(btnIndependiente) btnIndependiente.classList.add('active');

    } else if (rol === 'ADMINISTRADOR' || rol === 'BARBERO') {
        currentRole = 'ADMINISTRADOR';
        if(groupNegocio) groupNegocio.style.display = 'block';
        if(groupEspecialidad) groupEspecialidad.style.display = 'block';
        if(groupFotoRostro) groupFotoRostro.style.display = 'none';
        if(groupDescripcion) groupDescripcion.style.display = 'none';
        if(sectionReserva) sectionReserva.style.display = 'none';
        if(inputNegocio) inputNegocio.required = true;
        
        btnText.innerText = "Registrar mi Negocio";
        if(btnBarbero) btnBarbero.classList.add('active');

    } else {
        currentRole = 'CLIENTE';
        if(groupNegocio) groupNegocio.style.display = 'none';
        if(groupEspecialidad) groupEspecialidad.style.display = 'none';
        if(groupFotoRostro) groupFotoRostro.style.display = 'none';
        if(groupDescripcion) groupDescripcion.style.display = 'none';
        
        if(qrProveedorId && sectionReserva) sectionReserva.style.display = 'block';
        if(inputNegocio) inputNegocio.required = false;
        
        btnText.innerText = "Registrarme como Cliente";
        if(btnCliente) btnCliente.classList.add('active');
    }
}

/**
 * 🚩 Lógica de Domicilio (Ocultar/Mostrar dirección)
 */
function toggleDireccionRegistro() {
    const modalidad = document.getElementById('regModalidad').value;
    const groupDireccion = document.getElementById('groupDireccionRegistro');
    const inputDireccion = document.getElementById('regDireccion');
    
    if (modalidad === 'domicilio') {
        if(groupDireccion) groupDireccion.style.display = 'block';
        if(inputDireccion) inputDireccion.required = true;
    } else {
        if(groupDireccion) groupDireccion.style.display = 'none';
        if(inputDireccion) inputDireccion.required = false;
    }
}

/**
 * 🚩 Cargar Servicios del Proveedor si viene por QR
 */
async function inicializarFlujoQR() {
    if (!qrProveedorId) return;

    const sectionReserva = document.getElementById('sectionReservaInmediata');
    const divNegocio = document.getElementById('negocioDetected');
    const txtNegocio = document.getElementById('nombreNegocioQR');
    
    if(sectionReserva) sectionReserva.style.display = 'block';
    if(divNegocio) divNegocio.style.display = 'block';

    try {
        const response = await fetch(`${API_HOST}/api/Servicios/proveedor/${qrProveedorId}`);
        if (response.ok) {
            const servicios = await response.json();
            const selectServicio = document.getElementById('regServicio');
            if(selectServicio) {
                selectServicio.innerHTML = servicios.map(s => `<option value="${s.id}">${s.nombre} - $${s.precio}</option>`).join('');
            }
            
            const respProv = await fetch(`${API_HOST}/api/Proveedores/${qrProveedorId}`);
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
        const response = await fetch(`${API_HOST}/api/Citas/disponibilidad?proveedorId=${qrProveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        if (response.ok) {
            const horas = await response.json();
            if(selectHora) {
                selectHora.innerHTML = horas.map(h => `<option value="${h}">${h.slice(0, 5)}</option>`).join('');
            }
        }
    } catch (e) { console.error("Error disponibilidad", e); }
});

/**
 * Manejador principal del Registro (CON SOPORTE DE PROFESIONAL INDEPENDIENTE HU-10)
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

    // 🚀 HU-10 CA2: Validación de Foto Obligatoria para Profesional Independiente
    if (currentRole === 'INDEPENDIENTE') {
        const inputFoto = document.getElementById('regFotoRostro');
        if (!inputFoto || !inputFoto.files || inputFoto.files.length === 0) {
            alert("⚠️ La foto del rostro es obligatoria para registrarte como Profesional Independiente.");
            return;
        }
    }

    btnSubmit.disabled = true;
    btnSubmit.innerText = "Procesando Registro...";

    try {
        // =========================================================================
        // 🚀 CASO A: REGISTRO DE PROFESIONAL INDEPENDIENTE (HU-10) -> Endpoint Form-Data
        // =========================================================================
        if (currentRole === 'INDEPENDIENTE') {
            const formData = new FormData();
            formData.append('Nombre', document.getElementById('regNombre').value.trim());
            formData.append('Email', document.getElementById('regEmail').value.trim());
            formData.append('Password', password);
            formData.append('Telefono', document.getElementById('regTelefono').value.trim());
            formData.append('Categoria', document.getElementById('regCategoria')?.value || "Barbero");
            formData.append('Descripcion', document.getElementById('regDescripcion')?.value.trim() || "");
            formData.append('Direccion', "Atención a domicilio");
            formData.append('Ciudad', "Bogotá");
            
            const fileInput = document.getElementById('regFotoRostro');
            if (fileInput.files.length > 0) {
                formData.append('FotoRostro', fileInput.files[0]);
            }

            const response = await fetch(`${API_HOST}/api/Auth/registro-independiente`, {
                method: 'POST',
                body: formData
            });

            let result = { message: "No se pudo completar el registro de independiente." };
            const contentType = response.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                result = await response.json();
            } else {
                const textFallback = await response.text();
                result.message = textFallback || result.message;
            }

            if (response.ok) {
                // 🚀 CA4: Guardar Token e Iniciar Sesión Automáticamente
                if (result.token) {
                    localStorage.setItem('turnify_token', result.token);
                    localStorage.setItem('turnify_user', JSON.stringify(result.user));
                    alert("🚀 ¡Bienvenido a Turnify! Registro exitoso como Profesional Independiente.");
                    window.location.href = 'index.html'; // Redirección directa al Dashboard
                    return;
                }
                
                alert("🚀 Registro completado. Por favor inicia sesión.");
                window.location.href = 'login.html';
            } else {
                let errorMsg = result.message || "Error al registrar profesional independiente.";
                if(result.errors) {
                    errorMsg = Object.values(result.errors).flat().join("\n");
                }
                alert("❌ Error: " + errorMsg);
                btnSubmit.disabled = false;
                btnSubmit.innerText = "Registrarme como Independiente";
            }
            return;
        }

        // =========================================================================
        // 📦 CASO B: REGISTRO STANDARD (CLIENTE / ADMINISTRADOR DE NEGOCIO)
        // =========================================================================
        const registroData = {
            nombre: document.getElementById('regNombre').value.trim(),
            email: document.getElementById('regEmail').value.trim(),
            password: password,
            rol_id: currentRole === 'CLIENTE' ? ROLES.CLIENTE : ROLES.ADMINISTRADOR,
            telefono: document.getElementById('regTelefono').value.trim(),
            nombreComercial: currentRole === 'ADMINISTRADOR' ? document.getElementById('regNegocio').value.trim() : "",
            tipoNegocio: currentRole === 'ADMINISTRADOR' ? (document.getElementById('regCategoria')?.value || "Barbería") : "Particular"
        };

        const response = await fetch(`${API_HOST}/api/Usuarios/registrar`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(registroData)
        });

        let result = { message: "No se pudo completar el registro." };
        const contentType = response.headers.get("content-type");
        if (contentType && contentType.includes("application/json")) {
            result = await response.json();
        } else {
            const textFallback = await response.text();
            result.message = textFallback || result.message;
        }

        if (response.ok) {
            const clienteId = result.usuarioId || result.id;

            // PASO B.1: SI ES FLUJO QR, AGENDAMOS DE UNA VEZ
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

                const resCita = await fetch(`${API_HOST}/api/Citas/agendar`, {
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
            let errorMsg = result.message || "No se pudo completar el registro.";
            if(result.errors) {
                errorMsg = Object.values(result.errors).flat().join("\n");
            }
            alert("❌ Error: " + errorMsg);
            
            btnSubmit.disabled = false;
            btnSubmit.innerText = currentRole === 'CLIENTE' ? "Registrarme como Cliente" : "Registrar mi Negocio";
        }

    } catch (error) {
        console.error("🚨 Error:", error);
        alert("🔌 Error de conexión con el servidor.");
        btnSubmit.disabled = false;
        btnSubmit.innerText = "Reintentar Registro";
    }
});

// Inicializamos si hay QR presente al cargar la página
if(qrProveedorId) inicializarFlujoQR();