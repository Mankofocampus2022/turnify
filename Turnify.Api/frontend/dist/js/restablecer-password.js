/* ============================================================
   TURNIFY - MOTOR DE RESTABLECIMIENTO DE IDENTIDAD (AUTH FLUX)
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el origen de red en caliente. Si corre localmente usa el puerto 5000 de .NET,
// si entran desde una IP local (ej: pruebas en celular) o dominio en producción, reconfigura el host de inmediato.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

// 🔄 Esperamos a que todo el DOM (HTML) se haya cargado por completo antes de ejecutar el script
document.addEventListener('DOMContentLoaded', () => {
    console.log("🚀 [Turnify Debug] DOM cargado. Iniciando script de restablecimiento...");

    // 🚩 RESCATE AUTOMÁTICO DE TOKEN DESDE LA URL
    // Obtenemos los parámetros de búsqueda de la URL (ej: ?token=mi_codigo_secreto)
    const urlParams = new URLSearchParams(window.location.search);
    
    // 🛡️ MEJORA DE INFRAESTRUCTURA: Soporte flexible para mayúsculas/minúsculas (?token= o ?Token=)
    let tokenFromUrl = urlParams.get('token') || urlParams.get('Token');

    // 🛡️ ENLACE DE RESPALDO: Si el token viene después de un hash fragment (#token=) por ruteo SPA, lo capturamos también
    if (!tokenFromUrl && window.location.hash) {
        const hashParams = new URLSearchParams(window.location.hash.substring(1));
        tokenFromUrl = hashParams.get('token') || hashParams.get('Token');
    }

    // Evaluamos si el token existe en la URL
    const tokenInput = document.getElementById('reset-token');
    if (tokenInput) {
        if (tokenFromUrl) {
            console.log("🔑 [Turnify Debug] Token encontrado en la URL:", tokenFromUrl);
            // Asignamos el valor del token al input oculto en el formulario
            tokenInput.value = tokenFromUrl.trim(); // Sanitizado sin espacios
        } else {
            // Alerta en la consola en caso de que el enlace se abra sin token
            console.warn("⚠️ [Turnify Debug] Token de recuperación no encontrado en la URL.");
            // Opcional: Podrías asignar un valor vacío explícito o dejarlo como está
            tokenInput.value = "";
        }
    }
});

// 📩 Escuchamos el evento de envío (submit) del formulario
const formReset = document.getElementById('form-reset');
if (formReset) {
    formReset.addEventListener('submit', async (e) => {
        // 🛑 Evitamos que la página se recargue automáticamente al enviar el formulario
        e.preventDefault();
        
        console.log("📥 [Turnify Debug] Formulario enviado. Capturando datos del usuario...");

        // Capturamos los valores de los inputs del formulario
        // 🛡️ BLINDAJE ANTI-BUG: Aplicamos .trim() para limpiar espacios fantasmas del Copy-Paste que arruinan la Validación Dual en DB
        const email = document.getElementById('email').value.trim();
        const telefono = document.getElementById('telefono').value.trim();
        const token = document.getElementById('reset-token').value.trim();
        const password = document.getElementById('new-password').value;

        // Imprimimos en consola los datos capturados (Ocultando la contraseña por seguridad)
        console.log("📋 [Turnify Debug] Datos a enviar:", {
            email: email,
            telefono: telefono,
            token: token || "TOKEN_VACIO",
            password: "[OCULTO POR SEGURIDAD]"
        });

        // 🛡️ Validación de seguridad previa: Verificamos si el token está vacío
        if (!token) {
            console.warn("⚠️ [Turnify Debug] Advertencia: El token está vacío. Se intentará validación dual.");
        }

        try {
            console.log("🌐 [Turnify Debug] Enviando petición POST al backend...");

            // 🚩 [BLINDAJE DOCKER PRO]: Reemplazamos el string estático por la ruta dinámica autodetectada
            const TARGET_URL = `${API_HOST}/api/Usuarios/reset-password`;

            // Hacemos la petición HTTP POST al endpoint del backend
            const response = await fetch(TARGET_URL, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json' 
                },
                // 🚩 AJUSTE PASO 2: Sincronización con JsonPropertyName del DTO
                // Usamos minúsculas para que coincida con [JsonPropertyName("email")], etc.
                body: JSON.stringify({ 
                    email: email, 
                    telefono: telefono, 
                    token: token, 
                    newPassword: password 
                })
            });

            console.log(`📡 [Turnify Debug] Respuesta del servidor recibida. Status: ${response.status}`);

            // Si el backend responde con éxito (Status code 200-299)
            if (response.ok) {
                console.log("✅ [Turnify Debug] Contraseña actualizada exitosamente en el servidor.");
                alert("✅ Contraseña actualizada con éxito. Ya puedes iniciar sesión.");
                // Redirigimos al usuario al login
                window.location.href = 'login.html';
            } else {
                // 🛡️ BLINDAJE ANTI-CRASH DE QA SENIOR: Validamos si la respuesta es JSON real antes de parsear
                let err = { message: "Datos incorrectos o token expirado." };
                const contentType = response.headers.get("content-type");
                
                if (contentType && contentType.includes("application/json")) {
                    err = await response.json();
                } else {
                    const textFallback = await response.text();
                    err.message = textFallback || err.message;
                }

                console.error("⚠️ [Turnify Debug] Error devuelto por el servidor:", err);
                
                // Si el error viene del blindaje de .NET, mostramos un mensaje más amigable
                let mensajeError = err.message || "Datos incorrectos o token expirado.";
                if (err.errors) {
                    mensajeError = "Por favor verifica que todos los campos estén llenos correctamente.";
                }
                alert(`❌ Error: ${mensajeError}`);
            }
        } catch (err) {
            // En caso de que falle la conexión con la API (ej: Backend apagado)
            console.error("🔥 [Turnify Debug] Error crítico de conexión:", err);
            alert("❌ No se pudo conectar con el servidor.");
        }
    });
}

// 🛠️ Bloque de mantenimiento de espacio y comentarios adicionales para diagnóstico
// Fin del script de restablecimiento de contraseña para Turnify API