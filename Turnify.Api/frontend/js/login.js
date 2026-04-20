// 🛡️ CORRECCIÓN: En .NET el estándar es singular 'Usuario'. 
// Si tu controlador es 'UsuarioController', quítale la 's' a 'Usuarios'.
const API_URL = 'http://localhost:5000/api/Usuarios/login';
async function login() {
    const btn = document.getElementById('btnEntrar');
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');

    const email = emailInput.value.trim();
    const password = passwordInput.value.trim();

    if (!email || !password) {
        alert("⚠️ Por favor, ingresa correo y contraseña.");
        return;
    }

    // Estado de carga (UX)
    btn.disabled = true;
    btn.innerText = "Cargando...";

    try {
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Accept': 'application/json' 
            },
            body: JSON.stringify({ 
                Email: email, 
                Password: password 
            })
        });

        // --- 🛡️ AJUSTE SENIOR (SIN CAMBIAR TU LÓGICA) ---
        // Verificamos que la respuesta sea OK antes de procesar el JSON
        if (response.ok) {
            const data = await response.json();
            console.log("✅ Login exitoso");

            // 1. Guardar en LocalStorage para toda la sesión
            localStorage.setItem('turnify_token', data.token);
            localStorage.setItem('usuario_nombre', data.user.nombre);
            
            // Guardamos el ROL (que viene como GUID según tu Swagger)
            const userRole = (data.user.rol || data.user.rolId || "").toUpperCase();
            localStorage.setItem('usuario_rol', userRole);

            // 2. Definición de Roles (Tus GUIDs de SQL Server)
            const SUPER_ADMIN = "6DE2A606-416E-4588-B4EB-CC20856CD80A"; // SuperAdministrador
            const ADMIN = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";       // Administrador

            // 3. Redirección inteligente
            if (userRole === SUPER_ADMIN || userRole === ADMIN) {
                window.location.href = 'admin-dashboard.html';
            } else {
                window.location.href = 'clientes.html';
            }

        } else {
            // Si el servidor responde error (401, 404, etc), evitamos el JSON.parse fallido
            let errorMsg = "Credenciales incorrectas o error en el servidor";
            
            // Intentamos leer el JSON de error solo si el Content-Type es correcto
            const contentType = response.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                const errorData = await response.json();
                errorMsg = errorData.message || errorMsg;
            }

            alert("❌ Acceso denegado: " + errorMsg);
            btn.disabled = false;
            btn.innerText = "Entrar";
        }

    } catch (error) {
        console.error("Error:", error);
        alert("🚀 Error de conexión: Verifica que la API esté corriendo en el puerto 5000 y que la ruta /api/Usuarios/login exista.");
        btn.disabled = false;
        btn.innerText = "Entrar";
    }
}

// Escuchar el evento click si el botón existe
document.addEventListener('DOMContentLoaded', () => {
    const loginBtn = document.getElementById('btnEntrar');
    if (loginBtn) {
        loginBtn.addEventListener('click', login);
    }
});