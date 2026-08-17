const badgeConsultaClientes = document.getElementById("badge-consultaclientes");
const sqlServerLabel = document.getElementById("sql-server-label");
const alertClientes = document.getElementById("alert-clientes");
const tablaClientes = document.getElementById("tabla-clientes");
const btnCargarClientes = document.getElementById("btn-cargar-clientes");

function setPill(el, estado, texto) {
  el.classList.remove("status-checking", "status-ok", "status-error");
  el.classList.add(`status-${estado}`);
  el.innerHTML = `<span class="dot"></span>${texto}`;
}

function mostrarAlerta(el, tipo, mensaje) {
  el.innerHTML = `<div class="alert alert-${tipo} py-2 mb-0">${mensaje}</div>`;
}

function limpiarAlerta(el) {
  el.innerHTML = "";
}

function formatearSaldo(saldo) {
  const numero = Number(saldo ?? 0);
  return numero.toLocaleString("es-CR", { style: "currency", currency: "CRC" });
}

async function verificarSaludDb() {
  setPill(badgeConsultaClientes, "checking", "verificando…");
  try {
    const resp = await fetch(`${CONFIG.BASE_URL}/health/db`);
    const data = await resp.json();
    if (resp.ok) {
      sqlServerLabel.textContent = `${data.server}`;
      setPill(badgeConsultaClientes, "ok", "conectado");
    } else {
      setPill(badgeConsultaClientes, "error", "sin conexion");
    }
  } catch (err) {
    setPill(badgeConsultaClientes, "error", "sin conexion");
  }
}

async function cargarClientes() {
  limpiarAlerta(alertClientes);
  tablaClientes.innerHTML = `<tr><td colspan="4" class="text-secondary text-center py-4">Cargando…</td></tr>`;

  try {
    const resp = await fetch(`${CONFIG.BASE_URL}/consultaclientes`);
    if (!resp.ok) {
      const err = await resp.json().catch(() => ({}));
      throw new Error(err.detail || `HTTP ${resp.status}`);
    }

    const clientes = await resp.json();

    if (!clientes.length) {
      tablaClientes.innerHTML = `<tr><td colspan="4" class="text-secondary text-center py-4">No hay clientes registrados</td></tr>`;
      return;
    }

    tablaClientes.innerHTML = clientes
      .map(
        (c) => `
        <tr>
          <td class="mono">${c.clienteId}</td>
          <td>${c.cedula ?? "-"}</td>
          <td>${c.nombre ?? "-"}</td>
          <td class="text-end mono">${formatearSaldo(c.saldo)}</td>
        </tr>`
      )
      .join("");
  } catch (err) {
    tablaClientes.innerHTML = `<tr><td colspan="4" class="text-secondary text-center py-4">Sin datos cargados todavía</td></tr>`;
    mostrarAlerta(alertClientes, "danger", `No se pudo cargar la lista: ${err.message}`);
  }
}

btnCargarClientes.addEventListener("click", cargarClientes);

verificarSaludDb();
cargarClientes();
