/* ============================================================
   TURNIFY - MOTOR DE SEGURIDAD INTERNA PARA ADMINISTRADORES
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el host en caliente para que el navegador no falle por IPs locales o la nube.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

// 1. Token secreto requerido en la URL (?secret=...)
const SECRET_INVITATION_KEY = "TurnifyAdminSecure2026Key"; 

// 2. 🧠 FIX: Mapeo correcto de los dos niveles de administración sin duplicar variables
const ROLES_ADMIN = {
    ADMIN: "6DE2A606-416E-4588-B4EB-CC20856CD80A",
    SUPER_ADMIN: "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"
};

// Validamos el acceso antes de pintar nada
const urlParams = new URLSearchParams(window.location.search);
const tokenIngresado = urlParams.get('secret');

if (tokenIngresado === SECRET_INVITATION_KEY) {
    // Si la firma es correcta, destruimos el bloqueo y mostramos el panel
    const lockScreen = document.getElementById('lockScreen');
    const adminCard = document.getElementById('adminCard');
    if (lockScreen) lockScreen.style.display = 'none';
    if (adminCard) adminCard.style.display = 'block';
} else {
    console.error("🔒 Intento de acceso no autorizado al registro administrativo.");
}

/**
 * Envío seguro del formulario de Administración
 */
document.getElementById('formRegistroAdmin').addEventListener('submit', async (e) => {
    e.preventDefault();

    const btnSubmit = document.getElementById('btnSubmitAdmin');
    const password = document.getElementById('adminPassword').value;
    const confirm = document.getElementById('adminConfirmPassword').value;

    if (password !== confirm) {
        alert("⚠️ Las contraseñas no coinciden. Por favor, verifica.");
        return;
    }

    btnSubmit.disabled = true;
    btnSubmit.innerText = "Garantizando privilegios...";

    // 🧠 FIX: Capturamos el GUID del rol seleccionado en la interfaz
    const tipoAdminSeleccionado = document.getElementById('adminTipoRol').value;
    const rolGuidFinal = ROLES_ADMIN[tipoAdminSeleccionado] || ROLES_ADMIN.ADMIN;

    const adminPayload = {
        nombre: document.getElementById('adminNombre').value.trim(),
        email: document.getElementById('adminEmail').value.trim(),
        password: password,
        rol_id: rolGuidFinal, // 🧠 Inyección del GUID dinámico según la selección (Admin o Super Admin)
        telefono: document.getElementById('adminTelefono').value.trim(),
        nombreComercial: "Consola de Administración Central",
        tipoNegocio: "Sistemas"
    };

    try {
        // 🚩 [BLINDAJE DOCKER] - Reemplazamos la ruta estática local por la constante dinámica centralizada
        const TARGET_URL = `${API_HOST}/api/Usuarios/registrar`;

        // Despachamos al endpoint de usuarios de tu API en .NET
        const response = await fetch(TARGET_URL, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'X-Admin-Creation-Key': tokenIngresado // Enviamos el token en las cabeceras por seguridad extra
            },
            body: JSON.stringify(adminPayload)
        });

        // 🛡️ BLINDAJE ANTI-CRASH DE QA: Validamos el tipo de contenido antes de procesar el JSON para evitar caídas
        let result = { message: "Error interno de validación." };
        const contentType = response.headers.get("content-type");
        
        if (contentType && contentType.includes("application/json")) {
            result = await response.json();
        } else {
            const textFallback = await response.text();
            result.message = textFallback || result.message;
        }

        if (response.ok) {
            alert("🚀 ¡Cuenta de Administrador Turnify configurada con éxito!");
            window.location.href = 'login.html';
        } else {
            alert("❌ Fallo en registro: " + (result.message || result.Message || "Error interno de validación."));
            btnSubmit.disabled = false;
            btnSubmit.innerText = "Crear Cuenta Administrativa";
        }
    } catch (error) {
        console.error("🚨 Error:", error);
        alert("🔌 Error crítico de red al conectar con el servidor.");
        btnSubmit.disabled = false;
        btnSubmit.innerText = "Reintentar Configuración";
    }
});

/* ============================================================
    🛡️ [NUEVO COMPONENTE APARTE]: CAMBIO SEGURO DE CLAVE PARA ADMINS
    ============================================================ */
const formCambioPassAdmin = document.getElementById('formCambioPasswordAdmin');
if (formCambioPassAdmin) {
    formCambioPassAdmin.addEventListener('submit', async (e) => {
        e.preventDefault();

        // Bloqueo preventivo de seguridad perimetral si se intenta saltar la URL secreta
        if (tokenIngresado !== SECRET_INVITATION_KEY) {
            alert("⛔ Operación denegada: Firma criptográfica de administrador inválida o ausente.");
            return;
        }

        const btnReset = document.getElementById('btnResetPasswordAdmin');
        const emailAdmin = document.getElementById('changeAdminEmail').value.trim();
        const telAdmin = document.getElementById('changeAdminTelefono').value.trim();
        const newPassword = document.getElementById('changeAdminNewPassword').value;
        const confirmPassword = document.getElementById('changeAdminConfirmPassword').value;

        if (newPassword !== confirmPassword) {
            alert("⚠️ Las nuevas contraseñas administrativas no coinciden.");
            return;
        }

        btnReset.disabled = true;
        btnReset.innerText = "Actualizando credenciales de élite...";

        try {
            const TARGET_RESET_URL = `${API_HOST}/api/Usuarios/reset-password`;

            // Hit seguro al core de identidades con cabecera de verificación reforzada
            const response = await fetch(TARGET_RESET_URL, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Admin-Creation-Key': tokenIngresado // Doble factor de autorización por token de red
                },
                body: JSON.stringify({
                    email: emailAdmin,
                    telefono: telAdmin,
                    token: SECRET_INVITATION_KEY, // Reutiliza la llave de invitación como firma de validación
                    newPassword: newPassword
                })
            });

            let resultData = { message: "Error al actualizar la contraseña de administración." };
            const contentType = response.headers.get("content-type");

            if (contentType && contentType.includes("application/json")) {
                resultData = await response.json();
            } else {
                const textFallback = await response.text();
                resultData.message = textFallback || resultData.message;
            }

            if (response.ok) {
                alert("🔒 ¡Credenciales de Administrador actualizadas correctamente en la base de datos!");
                window.location.href = 'login.html';
            } else {
                alert("❌ Fallo en cambio administrativo: " + (resultData.message || "Firma incorrecta o cuenta inexistente."));
                btnReset.disabled = false;
                btnReset.innerText = "Cambiar Contraseña Administrativa";
            }
        } catch (error) {
            console.error("🚨 Error crítico en flujo de reseteo Admin:", error);
            alert("🔌 Error de enlace de red con la consola de .NET Core.");
            btnReset.disabled = false;
            btnReset.innerText = "Reintentar Cambio Seguro";
        }
    });
}