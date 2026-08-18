// =================================================================
// DockerWebinar - lógica de front-end
// Cada microservicio (registro-cliente, registro-pago,
// consulta-clientes, consulta-pagos) se llama por separado y sus
// errores se manejan por separado: si uno cae, los otros tres
// siguen funcionando. Esto es justo lo que queremos evidenciar en
// el webinar.
// =================================================================

const money = (n) =>
  new Intl.NumberFormat("es-CR", { style: "currency", currency: "CRC" }).format(n || 0);

function setEstadoBadge(elId, online) {
  const el = document.getElementById(elId);
  el.classList.remove("status-checking", "status-online", "status-offline");
  if (online) {
    el.classList.add("status-online");
    el.innerHTML = '<span class="dot"></span>en línea';
  } else {
    el.classList.add("status-offline");
    el.innerHTML = '<span class="dot"></span>caído';
  }
}

function mostrarAlerta(slotId, tipo, mensaje) {
  const slot = document.getElementById(slotId);
  const icono = tipo === "success" ? "fa-circle-check" : "fa-triangle-exclamation";
  slot.innerHTML = `
    <div class="alert alert-${tipo} d-flex align-items-start gap-2 mb-0" role="alert">
      <i class="fa-solid ${icono} mt-1"></i>
      <div>${mensaje}</div>
    </div>`;
}

function limpiarAlerta(slotId) {
  document.getElementById(slotId).innerHTML = "";
}

// -----------------------------------------------------------------
// Extrae el detalle real del error devuelto por la API (Results.Problem
// manda { title, detail, status }), en vez de solo el código HTTP.
// -----------------------------------------------------------------
async function extraerDetalle(resp) {
  try {
    const body = await resp.json();
    return body.detail || body.title || `estado HTTP ${resp.status}`;
  } catch {
    return `estado HTTP ${resp.status}`;
  }
}

// -----------------------------------------------------------------
// Health checks independientes por microservicio
// -----------------------------------------------------------------
async function chequearServicio(url, badgeId) {
  try {
    const resp = await fetch(`${url}/health`, { signal: AbortSignal.timeout(4000) });
    setEstadoBadge(badgeId, resp.ok);
  } catch {
    setEstadoBadge(badgeId, false);
  }
}

function chequearTodos() {
  chequearServicio(CONFIG.REGISTRO_CLIENTE_URL, "badge-registrocliente");
  chequearServicio(CONFIG.REGISTRO_PAGO_URL, "badge-registropago");
  chequearServicio(CONFIG.CONSULTA_CLIENTES_URL, "badge-consultaclientes");
  chequearServicio(CONFIG.CONSULTA_PAGOS_URL, "badge-consultapagos");
}

// -----------------------------------------------------------------
// Registrar cliente -> microservicio registro-cliente (POST /registrocliente)
// -----------------------------------------------------------------
document.getElementById("form-cliente").addEventListener("submit", async (e) => {
  e.preventDefault();
  limpiarAlerta("alert-cliente");

  const payload = {
    cedula: document.getElementById("cliente-cedula").value.trim(),
    nombre: document.getElementById("cliente-nombre").value.trim(),
    saldo: parseFloat(document.getElementById("cliente-saldo").value) || 0
  };

  try {
    const resp = await fetch(`${CONFIG.REGISTRO_CLIENTE_URL}/registrocliente`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000)
    });

    if (!resp.ok) throw new Error(await extraerDetalle(resp));

    const data = await resp.json();
    mostrarAlerta("alert-cliente", "success", `Cliente #${data.clienteId} guardado correctamente.`);
    e.target.reset();
    setEstadoBadge("badge-registrocliente", true);
  } catch (err) {
    mostrarAlerta(
      "alert-cliente",
      "danger",
      `<strong>No se pudo registrar el cliente.</strong> El microservicio <span class="mono">registro-cliente</span> respondió, pero falló al escribir en SQL Server.<br><span class="mono small">${err.message}</span>`
    );
    setEstadoBadge("badge-registrocliente", false);
  }
});

// -----------------------------------------------------------------
// Registrar pago -> microservicio registro-pago (POST /registropago)
// -----------------------------------------------------------------
document.getElementById("form-pago").addEventListener("submit", async (e) => {
  e.preventDefault();
  limpiarAlerta("alert-pago");

  const payload = {
    clienteId: parseInt(document.getElementById("pago-clienteid").value, 10),
    monto: parseFloat(document.getElementById("pago-monto").value) || 0,
    referencia: document.getElementById("pago-referencia").value.trim(),
    estado: document.getElementById("pago-estado").value
  };

  try {
    const resp = await fetch(`${CONFIG.REGISTRO_PAGO_URL}/registropago`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000)
    });

    if (!resp.ok) throw new Error(await extraerDetalle(resp));

    const data = await resp.json();
    mostrarAlerta("alert-pago", "success", `Pago #${data.pagoId} guardado correctamente.`);
    e.target.reset();
    setEstadoBadge("badge-registropago", true);
  } catch (err) {
    mostrarAlerta(
      "alert-pago",
      "danger",
      `<strong>No se pudo registrar el pago.</strong> El microservicio <span class="mono">registro-pago</span> respondió, pero falló al escribir en SQL Server.<br><span class="mono small">${err.message}</span>`
    );
    setEstadoBadge("badge-registropago", false);
  }
});

// -----------------------------------------------------------------
// Consultar clientes -> microservicio consulta-clientes (GET /consultaclientes)
// -----------------------------------------------------------------
async function cargarClientes() {
  limpiarAlerta("alert-clientes");
  const tbody = document.getElementById("tabla-clientes");

  try {
    const resp = await fetch(`${CONFIG.CONSULTA_CLIENTES_URL}/consultaclientes`, {
      signal: AbortSignal.timeout(6000)
    });
    if (!resp.ok) throw new Error(await extraerDetalle(resp));

    const clientes = await resp.json();
    setEstadoBadge("badge-consultaclientes", true);

    if (!clientes.length) {
      tbody.innerHTML = `<tr><td colspan="4" class="text-secondary text-center py-4">No hay clientes registrados aún</td></tr>`;
      return;
    }

    tbody.innerHTML = clientes.map(c => `
      <tr>
        <td class="mono">${c.clienteId}</td>
        <td>${c.cedula ?? ""}</td>
        <td>${c.nombre ?? ""}</td>
        <td class="text-end">${money(c.saldo)}</td>
      </tr>`).join("");
  } catch (err) {
    setEstadoBadge("badge-consultaclientes", false);
    tbody.innerHTML = `<tr><td colspan="4" class="text-secondary text-center py-4">No se pudieron cargar los datos</td></tr>`;
    mostrarAlerta(
      "alert-clientes",
      "danger",
      `<strong>No se pudieron cargar los clientes.</strong> El microservicio <span class="mono">consulta-clientes</span> respondió, pero falló al leer de SQL Server.<br><span class="mono small">${err.message}</span>`
    );
  }
}

// -----------------------------------------------------------------
// Consultar pagos -> microservicio consulta-pagos (GET /consultapagos)
// -----------------------------------------------------------------
async function cargarPagos() {
  limpiarAlerta("alert-pagos");
  const tbody = document.getElementById("tabla-pagos");

  try {
    const resp = await fetch(`${CONFIG.CONSULTA_PAGOS_URL}/consultapagos`, {
      signal: AbortSignal.timeout(6000)
    });
    if (!resp.ok) throw new Error(await extraerDetalle(resp));

    const pagos = await resp.json();
    setEstadoBadge("badge-consultapagos", true);

    if (!pagos.length) {
      tbody.innerHTML = `<tr><td colspan="5" class="text-secondary text-center py-4">No hay pagos registrados aún</td></tr>`;
      return;
    }

    tbody.innerHTML = pagos.map(p => `
      <tr>
        <td class="mono">${p.pagoId}</td>
        <td>${p.clienteNombre ?? ""}</td>
        <td class="text-end">${money(p.monto)}</td>
        <td class="mono small">${p.fecha ? new Date(p.fecha).toLocaleString("es-CR") : ""}</td>
        <td><span class="badge bg-secondary">${p.estado ?? ""}</span></td>
      </tr>`).join("");
  } catch (err) {
    setEstadoBadge("badge-consultapagos", false);
    tbody.innerHTML = `<tr><td colspan="5" class="text-secondary text-center py-4">No se pudieron cargar los datos</td></tr>`;
    mostrarAlerta(
      "alert-pagos",
      "danger",
      `<strong>No se pudieron cargar los pagos.</strong> El microservicio <span class="mono">consulta-pagos</span> respondió, pero falló al leer de SQL Server.<br><span class="mono small">${err.message}</span>`
    );
  }
}

//Mostrar IP del Servidor SQL
async function mostrarServidorSql() {
  try {
    const resp = await fetch(`${CONFIG.CONSULTA_CLIENTES_URL}/health/db`, {
      signal: AbortSignal.timeout(4000)
    });
    const data = await resp.json();
    const label = document.getElementById("sql-server-label");
    if (data.server) {
      label.textContent = `${data.server}:1433`;
    }
  } catch {
    document.getElementById("sql-server-label").textContent = "no disponible";
  }
}


document.getElementById("btn-cargar-clientes").addEventListener("click", cargarClientes);
document.getElementById("btn-cargar-pagos").addEventListener("click", cargarPagos);
document.getElementById("btn-refresh").addEventListener("click", () => {
  chequearTodos();
  cargarClientes();
  cargarPagos();
});

// Carga inicial
chequearTodos();
mostrarServidorSql();
cargarClientes();
cargarPagos();

// Re-chequeo periódico del estado de los servicios
setInterval(chequearTodos, 15000);
