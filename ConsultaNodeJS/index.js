const express = require("express");
const path = require("path");
const sql = require("mssql");

const app = express();

app.use((req, res, next) => {
  res.header("Access-Control-Allow-Origin", "*");
  res.header("Access-Control-Allow-Methods", "*");
  res.header("Access-Control-Allow-Headers", "*");
  next();
});

// Vista web (HTML/CSS/JS estaticos), separada del sitio IIS.
// Se sirve desde el mismo contenedor que la API para no depender
// de un segundo servicio.
app.use(express.static(path.join(__dirname, "public")));

const sqlServer = process.env.SQL_SERVER || "host.docker.internal";
const sqlDatabase = process.env.SQL_DATABASE || "DockerWebinar";
const sqlUser = process.env.SQL_USER || "sa";
const sqlPassword = process.env.SQL_PASSWORD || "DockerDemo2026!";

const dbConfig = {
  server: sqlServer,
  port: 1433,
  database: sqlDatabase,
  user: sqlUser,
  password: sqlPassword,
  options: {
    trustServerCertificate: true,
    encrypt: false,
  },
  connectionTimeout: 30000,
};

// -----------------------------------------------------------------
// Abre la conexion a SQL Server con reintentos. La ruta de red de
// este laboratorio (contenedor Windows -> portproxy -> WSL2 ->
// contenedor Linux) puede perder el handshake inicial de forma
// intermitente; reintentar 2-3 veces es mas robusto que subir el
// timeout indefinidamente.
// -----------------------------------------------------------------
function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function abrirConexion() {
  let ultimoError = new Error("No se pudo abrir la conexion");
  for (let intento = 1; intento <= 3; intento++) {
    try {
      const pool = await sql.connect(dbConfig);
      return pool;
    } catch (ex) {
      ultimoError = ex;
      console.log(`[SQL] Intento ${intento}/3 fallido: ${ex.message}`);
      if (intento < 3) await delay(700);
    }
  }
  throw ultimoError;
}

app.get("/health", (req, res) => {
  res.json({ status: "ok", service: "consulta-clientes" });
});

app.get("/health/db", async (req, res) => {
  try {
    const pool = await abrirConexion();
    await pool.request().query("SELECT 1");
    res.json({ status: "ok", server: sqlServer, database: sqlDatabase });
  } catch (ex) {
    console.log(`[health/db] Fallo de conexion a ${sqlServer}: ${ex}`);
    res.status(500).json({
      title: "No se pudo conectar a SQL Server",
      detail: `${ex.name}: ${ex.message}`,
      status: 500,
    });
  }
});

// -----------------------------------------------------------------
// GET /consultaclientes -> Lista todos los clientes registrados
// -----------------------------------------------------------------
app.get("/consultaclientes", async (req, res) => {
  try {
    const pool = await abrirConexion();

    const resultado = await pool
      .request()
      .query("SELECT ClienteId, Cedula, Nombre, Saldo FROM Clientes ORDER BY ClienteId DESC");

    const lista = resultado.recordset.map((row) => ({
      clienteId: row.ClienteId,
      cedula: row.Cedula ?? null,
      nombre: row.Nombre ?? null,
      saldo: row.Saldo ?? 0,
    }));

    res.json(lista);
  } catch (ex) {
    console.log(`[consultaclientes] Error: ${ex}`);
    res.status(500).json({
      title: "No se pudo consultar clientes",
      detail: ex.message,
      status: 500,
    });
  }
});

const PORT = 8080;
app.listen(PORT, () => {
  console.log(`consulta-clientes (Node.js) escuchando en el puerto ${PORT}`);
});
