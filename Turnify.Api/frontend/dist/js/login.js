const API_URL = 'http://localhost:5000/api/Usuarios/login';

async function login() {
    const btn = document.getElementById('btnEntrar');
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value.trim();

    if (!email || !password) {
        alert("⚠️ Ingresa correo y contraseña.");
        return;
    }

    btn.disabled = true;
    btn.innerText = "Verificando...";

    try {
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Email: email, Password: password })
        });

        const data = await response.json();

        if (response.ok) {
            console.log("✅ LOGIN EXITOSO");
            localStorage.clear();

            // Guardado de datos
            localStorage.setItem('token', data.token);
            localStorage.setItem('adminName', data.user.nombre);
            localStorage.setItem('usuario_id', data.user.id);
            localStorage.setItem('proveedorId', data.user.proveedorId || data.user.id);
            
            // NORMALIZACIÓN DE ROL
            const userRole = String(data.user.rol || "").toUpperCase().trim();
            localStorage.setItem('usuario_rol', userRole);

            // GUIDS DE TU BASE DE DATOS
            const GUID_ADMIN = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";
            const GUID_SUPER = "6DE2A606-416E-4588-B4EB-CC20856CD80A";

            console.log("🔍 Rol detectado:", userRole);

            // LOGICA DE REDIRECCIÓN (CORTO CIRCUITO)
            if (userRole === GUID_ADMIN || userRole === GUID_SUPER || userRole === "SUPERADMINISTRADOR") {
                console.log("🚀 ES ADMIN. Redirigiendo...");
                window.location.href = "admin-dashboard.html";
                return; // IMPORTANTE: Corta la función aquí
            } 
            
            // Si no entró al IF de arriba, por defecto es cliente
            console.log("👤 ES CLIENTE. Redirigiendo...");
            window.location.href = "clientes.html";
            return;

        } else {
            alert("❌ " + (data.message || "Credenciales incorrectas"));
        }
    } catch (error) {
        console.error("🔥 Error:", error);
        alert("🚀 El servidor no responde.");
    } finally {
        btn.disabled = false;
        btn.innerText = "Entrar al Panel";
    }
}

// Eventos
document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('btnEntrar').addEventListener('click', login);
    document.addEventListener('keypress', (e) => { if (e.key === 'Enter') login(); });
});