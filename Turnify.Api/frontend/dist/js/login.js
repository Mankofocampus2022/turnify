/**
 * TURNIFY - Módulo de Autenticación Profesional
 * Desarrollado por: Darwin Ocampo
 * Descripción: Maneja el inicio de sesión, persistencia de JWT y redirección por roles.
 */

// Configuración global del endpoint
const API_URL = 'http://localhost:5000/api/Usuarios/login';

document.addEventListener('DOMContentLoaded', () => {
    // Referencias a los elementos del DOM (Interfaz de usuario)
    const btnEntrar = document.getElementById('btnEntrar');
    const loginForm = document.getElementById('formLogin');
    const alertBox = document.getElementById('login-alert');

    /**
     * Muestra alertas visuales en el DOM sin bloquear el hilo de ejecución
     * @param {string} message - Mensaje a mostrar
     * @param {boolean} isError - Define si es un error o éxito
     */
    const showAlert = (message, isError = true) => {
        if (alertBox) {
            alertBox.textContent = message;
            // Aplicamos clases CSS para feedback visual inmediato
            alertBox.className = isError ? 'alert-error' : 'alert-success';
            // Auto-ocultamiento para mantener la UI limpia
            setTimeout(() => alertBox.className = 'alert-hidden', 5000);
        } else {
            alert(message); // Respaldo por si el div no existe en el HTML
        }
    };

    /**
     * Función principal de Login (Asíncrona)
     * Ejecuta la petición POST al API y gestiona la sesión
     */
    const handleLogin = async (event) => {
        // Detenemos el envío tradicional del formulario para manejarlo por JS (AJAX/Fetch)
        if (event) event.preventDefault();

        // Captura de datos del formulario con limpieza de espacios en blanco
        const email = document.getElementById('email').value.trim();
        const password = document.getElementById('password').value;
        const userType = document.getElementById('userType').value;

        // Validación de campos obligatorios antes de disparar la red
        if (!email || !password) {
            showAlert("⚠️ Ingresa correo y contraseña.");
            return;
        }

        // Bloqueo de UI para prevenir múltiples peticiones (Doble clic)
        btnEntrar.disabled = true;
        const originalText = btnEntrar.textContent;
        btnEntrar.textContent = "Verificando...";

        try {
            console.log("🚀 Iniciando petición POST a:", API_URL);
            
            // Petición al servidor .NET
            const response = await fetch(API_URL, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'accept': '*/*' // Asegura que el servidor acepte la respuesta
                },
                // Cuerpo de la petición: El backend espera estas llaves en minúsculas o mayúsculas
                body: JSON.stringify({ email: email, password: password })
            });

            // Procesamiento de la respuesta JSON
            const data = await response.json();
            console.log("🔥 DATA RECIBIDA DEL API:", data);

            if (response.ok) {
                console.log("✅ Autenticación correcta. Iniciando persistencia en LocalStorage...");
                
                // --- BLINDAJE DE PERSISTENCIA ---
                // Limpiamos cualquier rastro de sesiones anteriores para evitar conflictos
                localStorage.clear();

                // 1. Persistencia del Token JWT (Llave de acceso a rutas protegidas)
                const token = data.token || data.accessToken || "";
                localStorage.setItem('token', token);

                // 2. Extracción segura del objeto Usuario (Soporta múltiples nomenclaturas)
                const user = data.user || data.usuario || {};
                localStorage.setItem('usuario_id', user.id || "");
                localStorage.setItem('adminName', user.nombre || "");
                
                // 3. Persistencia del ID de Proveedor (Crítico para filtrar turnos en el Dashboard)
                const proveedorId = user.proveedorId || user.id || "";
                localStorage.setItem('proveedorId', proveedorId);
                
                // 4. Normalización de Rol para lógica de redirección
                // Si el rol viene como objeto, extraemos el nombre. Si es string, lo usamos.
                const rawRole = (user.rol && typeof user.rol === 'object') ? user.rol.nombre : user.rol;
                const userRole = String(rawRole || "").toUpperCase().trim();
                
                // Guardamos bajo ambas llaves para compatibilidad con scripts antiguos y nuevos
                localStorage.setItem('usuario_role', userRole); 
                localStorage.setItem('usuario_rol', userRole);

                // TUS GUIDS DE SEGURIDAD (Inyectados desde la base de datos)
                const GUID_ADMIN = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";
                const GUID_SUPER = "6DE2A606-416E-4588-B4EB-CC20856CD80A";

                // Logs de auditoría interna
                console.log("🔑 Token guardado con éxito:", !!localStorage.getItem('token'));
                console.log("🆔 ProveedorId mapeado:", localStorage.getItem('proveedorId'));
                console.log("🔍 Rol normalizado para lógica:", userRole);

                // --- SISTEMA DE REDIRECCIÓN CONTROLADA ---
                /**
                 * El delay de 150ms es un blindaje necesario para máquinas virtuales (VirtualBox)
                 * Asegura que el navegador escriba físicamente el localStorage antes de saltar de página.
                 */
                const redirect = (url) => {
                    console.log("✈️ Redirigiendo a:", url);
                    setTimeout(() => { window.location.href = url; }, 150);
                };

                // Evaluación de permisos por GUID o por Nombre de Rol
                if (userRole === GUID_ADMIN || 
                    userRole === GUID_SUPER || 
                    userRole.includes("ADMIN") || 
                    userRole.includes("SUPERADMIN")) {
                    redirect("admin-dashboard.html");
                } 
                else if (userRole.includes("PROVEEDOR")) {
                    redirect("dashboard.html");
                } 
                else {
                    redirect("clientes.html");
                }

            } else {
                // Manejo de errores controlados por el API (401, 404, etc)
                showAlert("❌ " + (data.message || "Credenciales incorrectas. Revisa tu correo y contraseña."));
                btnEntrar.disabled = false;
                btnEntrar.textContent = originalText;
            }
        } catch (error) {
            // Manejo de desastres (Servidor caído, errores de red o CORS)
            console.error("🔥 Error crítico de red:", error);
            showAlert("🚀 El servidor no responde. ¿Docker está corriendo en el puerto 5000?");
            btnEntrar.disabled = false;
            btnEntrar.textContent = originalText;
        }
    };

    // --- ASIGNACIÓN DE EVENTOS ---
    // Si existe el formulario, usamos el evento 'submit' (soporta tecla Enter)
    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    } 
    // Si no hay formulario, usamos el clic tradicional del botón
    else if (btnEntrar) {
        btnEntrar.addEventListener('click', handleLogin);
    }
});