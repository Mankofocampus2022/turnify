/* ============================================================
   TURNIFY - MOTOR DE AGENDAMIENTO Y DISPONIBILIDAD HORARIA
   ============================================================ */

// 🛡️ CACHÉ GLOBAL DE PROVEEDORES PARA EVALUACIÓN DUAL (HU-22 / CA1 / CA4)
window.listaProveedoresCache = [];

document.addEventListener('DOMContentLoaded', async () => {
    // 🛡️ [NUEVO] DETECCIÓN DE CÓDIGO QR / AISLAMIENTO DE MULTI-TENANT (Extracción Temprana)
    const urlParams = new URLSearchParams(window.location.search);
    const qrProveedorId = urlParams.get('id');

    // 🛡️ Blindaje de sesión: Soporta múltiples llaves de token por compatibilidad
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const userStr = localStorage.getItem('user');
    
    // 🚀 FIX: Si NO hay QR y NO hay sesión, forzamos login. Si hay QR, habilitamos Guest Checkout.
    if (!qrProveedorId && (!token || !userStr)) {
        window.location.href = 'login.html';
        return;
    }

    // Estructura segura contra nulabilidad en modo incógnito
    const user = userStr ? JSON.parse(userStr) : { id: '00000000-0000-0000-0000-000000000000' };
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase().trim();
    const esCliente = rol.includes("CLIENTE");

    if (qrProveedorId) {
        // 🚀 OBSERVACIÓN EXCEL MATADA: Machetazo visual inmediato al Sidebar y Header administrativo
        const sidebar = document.querySelector('.sidebar') || document.getElementById('sidebar');
        const headerAdmin = document.querySelector('.header-admin') || document.querySelector('header');
        const mainContent = document.querySelector('.main-content') || document.querySelector('.wrapper');
        
        if (sidebar) sidebar.style.display = 'none';
        if (headerAdmin) headerAdmin.style.display = 'none';
        
        // Forzamos al formulario a tomar todo el ancho real de la pantalla del celular/PC
        if (mainContent) {
            mainContent.style.marginLeft = '0';
            mainContent.style.width = '100%';
            mainContent.style.padding = '15px';
            mainContent.style.boxShadow = 'none';
        }

        // Si no hay sesión activa, visualizamos el formulario de contacto para invitados
        if (!token) {
            const secAnonimo = document.getElementById('sectionClienteAnonimo');
            if (secAnonimo) secAnonimo.style.display = 'block';
        }
    }

    // 🚩 URL Dinámica: Blindada para Docker y entornos productivos (Matriz de Red Inteligente)
    let API_BASE = window.location.origin + '/api';
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        // Si accedes a través de una IP de Red Local (ej. Pruebas desde el celular), mapea al puerto 5000
        API_BASE = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }

    console.log("🚀 [Turnify Log] Rol Detectado:", rol);

    // 🚩 LÓGICA DE INTERFAZ POR ROL (Mantenemos tu estructura intacta)
    const sectionCliente = document.getElementById('sectionSeleccionarCliente');
    const sectionProveedor = document.getElementById('sectionSeleccionarProveedor');
    const subtitulo = document.getElementById('subtituloAgendar');
    
    // 🚀 HU 001 - MULTI-SILLA: Referencias a los bloques de interfaz correspondientes
    const sectionStaffEstacion = document.getElementById('sectionStaffEstacion');
    const sectionBarberoPreferido = document.getElementById('sectionBarberoPreferido');

    if (esCliente || qrProveedorId) {
        if (sectionCliente) sectionCliente.style.display = 'none';
        if (sectionProveedor) sectionProveedor.style.display = qrProveedorId ? 'none' : 'block';
        if (sectionStaffEstacion) sectionStaffEstacion.style.display = 'none'; // El cliente externo nunca ve sillas
        if (sectionBarberoPreferido) sectionBarberoPreferido.style.display = 'block'; // El estado se evalúa dinámicamente según si es independiente
        
        if (subtitulo && !qrProveedorId) subtitulo.innerText = "Reserva tu cita con tu profesional favorito.";
        
        if (!qrProveedorId) {
            cargarProveedores(token, API_BASE); 
        }
        
        // 🛡️ Cargamos historial para clientes con el ID correcto de la tabla Clientes
        if (token && (user.clienteId || user.id)) cargarMisCitas(user.clienteId || user.id, token, "cliente", API_BASE);
    } else {
        if (sectionCliente) sectionCliente.style.display = 'block';
        if (sectionProveedor) sectionProveedor.style.display = 'none';
        if (sectionBarberoPreferido) sectionBarberoPreferido.style.display = 'none'; // El admin usa la sección detallada
        if (sectionStaffEstacion) sectionStaffEstacion.style.display = 'block'; // Jefes SI ven asignación manual de sillas
        if (subtitulo) subtitulo.innerText = "Registro manual de servicios (Local / Domicilio)";
        
        if (token) cargarClientes(token, API_BASE);
        const proveedorId = user.proveedorId || user.id;
        
        if (token && proveedorId && proveedorId !== '00000000-0000-0000-0000-000000000000') {
            cargarServicios(proveedorId, token, API_BASE);
            cargarMisCitas(proveedorId, token, "proveedor", API_BASE);
            
            // 🚀 HU 001: Cargar Barberos y Sillas disponibles para el panel administrativo del negocio
            cargarEmpleadosDelNegocio(token, API_BASE);
            cargarEstacionesDelNegocio(token, API_BASE);
        }
    }

    // 🛡️ [NUEVO OVERRIDE] Forzar visibilidad de agendamiento si entramos por código QR
    if (qrProveedorId) {
        if (subtitulo) {
            subtitulo.innerText = "Cargando información del establecimiento...";
        }
        // Forzamos la carga de proveedores de manera pública (con token nulo o real)
        cargarProveedores(token, API_BASE);
        // Inyectamos de inmediato los servicios del portafolio del QR
        cargarServicios(qrProveedorId, token, API_BASE);
    }

    // 🔥 [KILLER FIX] - VINCULACIÓN DE EVENTOS DE DISPONIBILIDAD
    const inputFecha = document.getElementById('citaFecha');
    const selectServicio = document.getElementById('citaServicioId');
    const selectModalidad = document.getElementById('citaModalidad');

    if (inputFecha) inputFecha.addEventListener('change', () => cargarDisponibilidad(API_BASE));
    if (selectServicio) selectServicio.addEventListener('change', () => cargarDisponibilidad(API_BASE));
    
    if (selectModalidad) {
        // 🚀 FIX VISUAL: Forzamos estilos oscuros en las opciones estáticas de modalidad para evitar blanco sobre blanco
        Array.from(selectModalidad.options).forEach(opt => {
            opt.style.backgroundColor = "#1a2238";
            opt.style.color = "#ffffff";
        });
        selectModalidad.addEventListener('change', () => {
            toggleDireccionCita();
            cargarDisponibilidad(API_BASE);
        });
    }

    const formCita = document.getElementById('formNuevaCita');
    if (formCita) {
        formCita.addEventListener('submit', (e) => guardarCita(e, token, user, rol, API_BASE));
    }
});

// --- [NUEVO] 📜 GESTIÓN DE MIS CITAS (VER, CANCELAR, CAMBIAR) ---
async function cargarMisCitas(id, token, tipo, API_BASE) {
    const container = document.getElementById('listaMisCitas'); 
    if (!container) return;

    console.log(`📡 [Fetch] Obteniendo agenda para ${tipo}...`);
    const url = tipo === "cliente" ? 
        `${API_BASE}/Citas/historial/${id}` : 
        `${API_BASE}/Citas/hoy`; 

    try {
        const resp = await fetch(url, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        
        if (resp.ok) {
            const contentType = resp.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                const citas = await resp.json();
                if (Array.isArray(citas)) {
                    renderizarCitas(citas, container, token, API_BASE);
                } else {
                    renderizarCitas([], container, token, API_BASE);
                }
            } else {
                renderizarCitas([], container, token, API_BASE);
            }
        } else {
            renderizarCitas([], container, token, API_BASE);
        }
    } catch (e) { 
        console.log("🔥 Error cargando mis citas:", e);
        container.innerHTML = "<p style='color: #ff5e5e; font-size: 0.7rem;'>Error al cargar agenda.</p>";
    }
}

// 🛡️ REPARACIÓN DE RENDERIZADO: Estilo Glass y Token de Seguridad
function renderizarCitas(citas, container, token, API_BASE) {
    if (!Array.isArray(citas) || citas.length === 0) {
        container.innerHTML = "<p style='color: rgba(255,255,255,0.5); font-size: 0.75rem; padding: 10px;'>No tienes citas registradas.</p>";
        return;
    }

    container.innerHTML = citas.map(c => `
        <div class="card-cita-sidebar ${c.estado ? c.estado.toLowerCase() : 'pendiente'}">
            ${c.estado === 'pendiente' ? `
                <button class="btn-cancelar-mini" onclick="cancelarCita('${c.id}', '${token}', '${API_BASE}')" title="Cancelar Cita">
                    <i class="fas fa-times-circle"></i>
                </button>
            ` : ''}
            <h6>${c.servicioNombre || "Servicio"}</h6>
            
            <div class="cita-info" style="font-weight: 600; color: #fff;">
                <i class="fas fa-store"></i> ${c.proveedorNombre || c.ProveedorNombre || "Establecimiento"}
            </div>

            <div class="cita-info" style="color: #cbd5e1; font-size: 0.75rem; margin-top: 3px;">
                <i class="fas fa-user-tie"></i> ${c.empleadoAsignado || c.EmpleadoAsignado || 'Sin asignar'}
                | <i class="fas fa-chair"></i> ${c.estacionAsignada || c.EstacionAsignada || 'Local'}
            </div>

            <div class="cita-info">
                <i class="far fa-calendar-alt"></i> ${c.fecha ? c.fecha.split('T')[0] : 'Hoy'}
            </div>
            <div class="cita-info">
                <i class="far fa-clock"></i> ${c.hora ? c.hora.toString().slice(0, 5) : '--:--'}
            </div>
            <div class="cita-info">
                <i class="fas fa-info-circle"></i> ${c.estado ? c.estado.toUpperCase() : 'PENDIENTE'}
            </div>
            ${c.codigoVerificacion ? `
                <div class="token-tag">
                    <i class="fas fa-key"></i> TOKEN: ${c.codigoVerificacion}
                </div>
            ` : ''}
        </div>
    `).join('');
}

// 🛡️ CANCELACIÓN BLINDADA (PATCH al endpoint correcto)
async function cancelarCita(id, token, API_BASE) {
    if (!confirm("⚠️ ¿Estás seguro de que deseas cancelar esta cita?")) return;

    try {
        const resp = await fetch(`${API_BASE}/Citas/${id}/estado`, {
            method: 'PATCH',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}` 
            },
            body: JSON.stringify({ nuevoEstado: 'cancelada' })
        });

        if (resp.ok) {
            alert("✅ Cita cancelada correctamente.");
            location.reload(); 
        } else {
            const errText = await resp.text();
            alert("❌ Error: " + errText);
        }
    } catch (e) { console.error("🔥 Error conexión cancelación:", e); }
}

/**
 * 🎯 EVALUAR MODALIDAD DEL PROVEEDOR (HU-22 / CA1 / CA4)
 * Oculta/muestra la sección de "Barbero/Especialista Preferido" según si el proveedor es independiente.
 */
function evaluarModoProveedor(proveedorId, API_BASE) {
    if (!proveedorId) return;

    const prov = window.listaProveedoresCache.find(p => (p.id || p.Id) === proveedorId);
    const sectionBarberoPreferido = document.getElementById('sectionBarberoPreferido');
    const selectClienteEmpleado = document.getElementById('citaClienteEmpleadoId');

    const esIndependiente = prov ? (prov.es_independiente || prov.EsIndependiente || prov.esIndependiente || false) : false;

    if (esIndependiente) {
        console.log("👤 [Turnify Log] Proveedor Independiente detectado -> Ocultando selector de staff.");
        if (sectionBarberoPreferido) sectionBarberoPreferido.style.display = 'none';
        if (selectClienteEmpleado) selectClienteEmpleado.value = "";
    } else {
        console.log("🏢 [Turnify Log] Establecimiento/Salón detectado -> Mostrando catálogo de staff.");
        const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
        const esCliente = rol.includes("CLIENTE");
        const urlParams = new URLSearchParams(window.location.search);
        const qrId = urlParams.get('id');

        if ((esCliente || qrId) && sectionBarberoPreferido) {
            sectionBarberoPreferido.style.display = 'block';
            cargarEmpleadosPublico(proveedorId, API_BASE);
        }
    }
}

/**
 * 🚩 CARGA DE PROVEEDORES
 */
async function cargarProveedores(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Proveedores?ignorePagination=true&t=${new Date().getTime()}`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });
        if (resp.ok) {
            const proveedores = await resp.json();
            window.listaProveedoresCache = proveedores || []; // 🛡️ Persistencia local para rápida evaluación
            
            const selectProv = document.getElementById('citaProveedorId');
            if (selectProv) {
                selectProv.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">-- Selecciona Profesional --</option>' + 
                    proveedores.map(p => {
                        const idFinal = p.id || p.Id;
                        const nombreFinal = p.nombre_comercial || p.nombreComercial || p.NombreComercial || p.nombre || p.Nombre || "Establecimiento";
                        const esIndep = (p.es_independiente || p.EsIndependiente || p.esIndependiente) ? " (Independiente)" : "";
                        return `<option value="${idFinal}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${nombreFinal}${esIndep}</option>`;
                    }).join('');
                
                const urlParams = new URLSearchParams(window.location.search);
                const qrId = urlParams.get('id');
                if (qrId) {
                    selectProv.value = qrId;
                    selectProv.disabled = true; 
                    
                    const provSeleccionado = proveedores.find(p => (p.id || p.Id) === qrId);
                    if (provSeleccionado && document.getElementById('subtituloAgendar')) {
                        const nombreQrFinal = provSeleccionado.nombre_comercial || provSeleccionado.nombreComercial || provSeleccionado.NombreComercial || provSeleccionado.nombre || provSeleccionado.Nombre || "Establecimiento";
                        document.getElementById('subtituloAgendar').innerText = `Agendando cita en: ${nombreQrFinal}`;
                    }
                    evaluarModoProveedor(qrId, API_BASE);
                }

                selectProv.onchange = () => {
                    const selectedId = selectProv.value;
                    cargarServicios(selectedId, token, API_BASE);
                    evaluarModoProveedor(selectedId, API_BASE);
                    const container = document.getElementById('containerSlots');
                    if (container) container.innerHTML = "";
                };
            }
        }
    } catch (e) { console.error("🔥 Error proveedores:", e); }
}

async function cargarServicios(proveedorId, token, API_BASE) {
    if (!proveedorId) return;
    try {
        const resp = await fetch(`${API_BASE}/Servicios/proveedor/${proveedorId}`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });

        if (resp.ok) {
            const servicios = await resp.json();
            const selectServicio = document.getElementById('citaServicioId');
            if (selectServicio) {
                selectServicio.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">Selecciona un servicio</option>' + 
                    servicios.map(s => `<option value="${s.id}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${s.nombre} ($${s.precio})</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error servicios:", e); }
}

async function cargarClientes(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Clientes`, {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
        });
        if (resp.ok) {
            const clientes = await resp.json();
            const selectCliente = document.getElementById('citaClienteId');
            if (selectCliente) {
                selectCliente.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">-- Buscar Cliente --</option>' + 
                    clientes.map(c => `<option value="${c.id}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${c.nombre} (${c.telefono})</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error clientes:", e); }
}

// 🚀 HU 001 - VISTA PÚBLICA: Cargar Barberos Activos usando el nuevo endpoint [AllowAnonymous]
async function cargarEmpleadosPublico(proveedorId, API_BASE) {
    if (!proveedorId) return;
    try {
        console.log("📡 [Fetch Público] Cargando catálogo de Staff para clientes...");
        const resp = await fetch(`${API_BASE}/Empleados/activos/${proveedorId}`);
        if (resp.ok) {
            const empleados = await resp.json();
            const selectClienteEmpleado = document.getElementById('citaClienteEmpleadoId');
            if (selectClienteEmpleado) {
                selectClienteEmpleado.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">-- Cualquier barbero disponible --</option>' + 
                    empleados.map(e => `<option value="${e.id}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${e.nombre}</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error cargando empleados públicos:", e); }
}

// 🚀 HU 001 - VISTA ADMIN: Carga completa interna para el Dueño
async function cargarEmpleadosDelNegocio(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/Empleados`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (resp.ok) {
            const empleados = await resp.json();
            const selectEmpleado = document.getElementById('citaEmpleadoId');
            if (selectEmpleado) {
                const activos = empleados.filter(e => e.activo);
                selectEmpleado.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">-- Cualquier barbero disponible --</option>' + 
                    activos.map(e => `<option value="${e.id}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${e.nombre} (${e.tipoContrato})</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error cargando empleados del negocio:", e); }
}

async function cargarEstacionesDelNegocio(token, API_BASE) {
    try {
        const resp = await fetch(`${API_BASE}/EstacionesTrabajo`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (resp.ok) {
            const estaciones = await resp.json();
            const selectEstacion = document.getElementById('citaEstacionId');
            if (selectEstacion) {
                const activas = estaciones.filter(e => e.activo);
                selectEstacion.innerHTML = '<option value="" style="background-color: #1a2238; color: #ffffff;">-- Cualquier silla libre --</option>' + 
                    activas.map(e => `<option value="${e.id}" style="background-color: #1a2238; color: #ffffff; padding: 10px;">${e.nombre}</option>`).join('');
            }
        }
    } catch (e) { console.error("🔥 Error cargando estaciones:", e); }
}

function toggleDireccionCita() {
    const modalidad = document.getElementById('citaModalidad').value;
    const groupDireccion = document.getElementById('groupDireccionCita');
    const inputDireccion = document.getElementById('citaDireccion');

    if (modalidad === 'domicilio') {
        if (groupDireccion) groupDireccion.style.display = 'block';
        if (inputDireccion) inputDireccion.required = true;
    } else {
        if (groupDireccion) groupDireccion.style.display = 'none';
        if (inputDireccion) {
            inputDireccion.required = false;
            inputDireccion.value = ""; 
        }
    }
}

// 🛡️ MOTOR DE DISPONIBILIDAD PRO
async function cargarDisponibilidad(API_BASE) {
    const fecha = document.getElementById('citaFecha').value;
    const servicioId = document.getElementById('citaServicioId').value;
    const userStr = localStorage.getItem('user');
    
    const urlParamsCheckDisp = new URLSearchParams(window.location.search);
    const qrIdCheckDisp = urlParamsCheckDisp.get('id');

    if ((!userStr && !qrIdCheckDisp) || !fecha || !servicioId) return;

    const user = userStr ? JSON.parse(userStr) : { id: '00000000-0000-0000-0000-000000000000' };
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    const selectProv = document.getElementById('citaProveedorId');
    const qrId = urlParamsCheckDisp.get('id');

    const proveedorId = qrId || (rol.includes("CLIENTE") ? 
        (selectProv ? selectProv.value : null) : 
        (user.proveedorId || user.id));

    const container = document.getElementById('containerSlots');
    if (!proveedorId) return;

    if (container) container.innerHTML = "<p><i class='fas fa-spinner fa-spin'></i> Calculando túnel de tiempo...</p>";

    try {
        const resp = await fetch(`${API_BASE}/Citas/disponibilidad?proveedorId=${proveedorId}&servicioId=${servicioId}&fecha=${fecha}`);
        const contentType = resp.headers.get("content-type");
        if (resp.ok && contentType && contentType.includes("application/json")) {
            const slots = await resp.json();
            if (container) {
                if(!slots || slots.length === 0) {
                    container.innerHTML = "<p style='color: #e94560;'>Sin disponibilidad.</p>";
                    return;
                }
                container.innerHTML = slots.map(h => 
                    `<div class="slot" onclick="seleccionarHora('${h}', this)">${h.slice(0, 5)}</div>`
                ).join('');
            }
        } else {
            const msgError = await resp.text();
            if (container) container.innerHTML = `<p style='color: #ff5e5e;'>${msgError || "Error de horario"}</p>`;
        }
    } catch (e) { 
        console.error("🔥 Error disponibilidad:", e); 
        if (container) container.innerHTML = "<p style='color: #ff5e5e;'>Error de red.</p>";
    }
}

function seleccionarHora(hora, elemento) {
    document.querySelectorAll('.slot').forEach(s => s.classList.remove('selected'));
    elemento.classList.add('selected');
    const inputHora = document.getElementById('citaHoraSeleccionada');
    if (inputHora) inputHora.value = hora;
}

// 🛡️ GUARDAR CITA
async function guardarCita(e, token, user, rol, API_BASE) {
    e.preventDefault();
    
    const inputHora = document.getElementById('citaHoraSeleccionada');
    let hora = inputHora ? inputHora.value : null;
    
    if (hora && hora.length === 5) {
        hora = `${hora}:00`;
    }

    const esCliente = rol.includes("CLIENTE");
    const selectCliente = document.getElementById('citaClienteId');
    const selectProv = document.getElementById('citaProveedorId');
    
    const urlParams = new URLSearchParams(window.location.search);
    const qrId = urlParams.get('id');

    if (token && !esCliente && qrId) {
        return alert("❌ Error de negocio: Un proveedor o administrador no puede solicitar citas para sí mismo. Por favor, cierra sesión o ingresa desde una cuenta de cliente.");
    }

    const clienteIdFinal = qrId ? (user.clienteId || user.id) : (esCliente ? user.clienteId : (selectCliente ? selectCliente.value : null));
    const proveedorIdFinal = qrId || (esCliente ? (selectProv ? selectProv.value : null) : (user.proveedorId || user.id));

    if (esCliente && !user.clienteId && !qrId) {
        return alert("❌ Error de Identidad: No se detectó tu ID de Cliente. Por favor, cierra sesión y vuelve a entrar.");
    }

    if (!clienteIdFinal || !proveedorIdFinal || !hora) return alert("⚠️ Completa todos los campos.");

    let anonNombre = document.getElementById('citaAnonimoNombre') ? document.getElementById('citaAnonimoNombre').value.trim() : "";
    let anonEmail = document.getElementById('citaAnonimoEmail') ? document.getElementById('citaAnonimoEmail').value.trim() : "";
    let anonWpp = document.getElementById('citaAnonimoWhatsApp') ? document.getElementById('citaAnonimoWhatsApp').value.trim() : "";

    if (!token && (!anonNombre || !anonEmail || !anonWpp)) {
        return alert("⚠️ Por favor completa tu Nombre, Correo y WhatsApp en el Paso 1 para poder procesar la reserva.");
    }

    if (token && anonNombre === "") {
        anonNombre = "Cliente Registrado Turnify";
        anonEmail = "usuario_autenticado@turnify.com";
        anonWpp = "3000000000";
    }

    // 🎯 VERIFICAR SI EL PROVEEDOR SELECCIONADO ES INDEPENDIENTE
    const provObj = window.listaProveedoresCache.find(p => (p.id || p.Id) === proveedorIdFinal);
    const esIndependiente = provObj ? (provObj.es_independiente || provObj.EsIndependiente || provObj.esIndependiente || false) : false;

    // 🚀 HU 001 - DISCRIMINACIÓN INTELIGENTE DE PREFERENCIA Y BLINDAJE CA4
    let empleadoIdVal = null;
    let estacionIdVal = null;

    if (esIndependiente) {
        // 🛡️ CA4: Si es independiente, se fuerza EmpleadoId a null
        empleadoIdVal = null;
        estacionIdVal = null;
    } else if (esCliente || qrId) {
        // Si es cliente y es salón/barbería, leemos el dropdown público de "Barbero Preferido"
        const selectFav = document.getElementById('citaClienteEmpleadoId');
        empleadoIdVal = (selectFav && selectFav.value !== "") ? selectFav.value : null;
        estacionIdVal = null;
    } else {
        // Si es administrador o dueño, leemos la asignación manual
        const empleadoSelect = document.getElementById('citaEmpleadoId');
        const estacionSelect = document.getElementById('citaEstacionId');
        empleadoIdVal = (empleadoSelect && empleadoSelect.value !== "") ? empleadoSelect.value : null;
        estacionIdVal = (estacionSelect && estacionSelect.value !== "") ? estacionSelect.value : null;
    }

    const btn = e.target.querySelector('button');
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Agendando...';

    const dto = {
        ClienteId: clienteIdFinal,
        ProveedorId: proveedorIdFinal,
        ServicioId: document.getElementById('citaServicioId').value,
        Fecha: document.getElementById('citaFecha').value,
        Hora: hora, 
        Modalidad: document.getElementById('citaModalidad').value,
        Direccion: document.getElementById('citaDireccion') ? document.getElementById('citaDireccion').value.trim() : "",
        Observaciones: document.getElementById('citaObservaciones') ? document.getElementById('citaObservaciones').value.trim() : "",
        MetodoRegistro: qrId ? "QR" : (esCliente ? "Web" : "Manual"),
        DuracionPactadaMin: 30, 
        
        AnonimoNombre: anonNombre,
        AnonimoEmail: anonEmail,
        AnonimoWhatsApp: anonWpp,

        // 🚀 HU 001 & CA4: Mapeo de IDs (Forzado a null si el proveedor es independiente)
        EmpleadoId: empleadoIdVal,
        EstacionId: estacionIdVal
    };

    try {
        const resp = await fetch(`${API_BASE}/Citas/agendar`, {
            method: 'POST',
            headers: token ? { 
                'Content-Type': 'application/json', 
                'Authorization': `Bearer ${token}` 
            } : {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(dto)
        });

        const contentType = resp.headers.get("content-type");
        if (contentType && contentType.includes("application/json")) {
            const data = await resp.json();
            if (resp.ok) {
                alert(`✅ ¡Cita Confirmada!\n\n${data.message}`); 
                window.location.reload(); 
            } else {
                let msgError = data.message || "Error al procesar la solicitud.";
                if (data.errors) {
                    msgError = Object.values(data.errors).flat().join("\n");
                }
                alert("❌ No se pudo agendar:\n" + msgError);
                btn.disabled = false;
                btn.innerHTML = originalHTML;
            }
        } else {
            const errorTexto = await resp.text();
            alert("❌ Error del Servidor: " + errorTexto);
            btn.disabled = false;
            btn.innerHTML = originalHTML;
        }
    } catch (e) { 
        alert("🔌 Error de conexión con el servicio de Turnify.");
        btn.disabled = false;
        btn.innerHTML = originalHTML;
    }
}

// PUENTE GLOBAL PARA HTML
window.seleccionarHora = seleccionarHora;
window.cancelarCita = cancelarCita;