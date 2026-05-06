// 🔄 Esperamos a que todo el DOM (HTML) se haya cargado por completo antes de ejecutar el script
document.addEventListener('DOMContentLoaded', () => {
    console.log("🚀 [Turnify Debug] DOM cargado. Iniciando script de restablecimiento...");

    // 🚩 RESCATE AUTOMÁTICO DE TOKEN DESDE LA URL
    // Obtenemos los parámetros de búsqueda de la URL (ej: ?token=mi_codigo_secreto)
    const urlParams = new URLSearchParams(window.location.search);
    const tokenFromUrl = urlParams.get('token');

    // Evaluamos si el token existe en la URL
    if (tokenFromUrl) {
        console.log("🔑 [Turnify Debug] Token encontrado en la URL:", tokenFromUrl);
        // Asignamos el valor del token al input oculto en el formulario
        document.getElementById('reset-token').value = tokenFromUrl;
    } else {
        // Alerta en la consola en caso de que el enlace se abra sin token
        console.warn("⚠️ [Turnify Debug] Token de recuperación no encontrado en la URL.");
        // Opcional: Podrías asignar un valor vacío explícito o dejarlo como está
        document.getElementById('reset-token').value = "";
    }
});

// 📩 Escuchamos el evento de envío (submit) del formulario
document.getElementById('form-reset').addEventListener('submit', async (e) => {
    // 🛑 Evitamos que la página se recargue automáticamente al enviar el formulario
    e.preventDefault();
    
    console.log("📥 [Turnify Debug] Formulario enviado. Capturando datos del usuario...");

    // Capturamos los valores de los inputs del formulario
    const email = document.getElementById('email').value;
    const telefono = document.getElementById('telefono').value;
    const token = document.getElementById('reset-token').value;
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

        // Hacemos la petición HTTP POST al endpoint del backend
        const response = await fetch('http://localhost:5000/api/Usuarios/reset-password', {
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
            // Si el backend responde con un error, capturamos el mensaje de error
            const err = await response.json();
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

// 🛠️ Bloque de mantenimiento de espacio y comentarios adicionales para diagnóstico
// Fin del script de restablecimiento de contraseña para Turnify API