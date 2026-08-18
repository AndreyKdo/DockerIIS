// ---------------------------------------------------------------
// Configuracion central de endpoints.
// Cada microservicio es independiente: si cambias el puerto o la
// URL de uno solo, edita únicamente esa línea (no hace falta
// reconstruir la imagen; basta con editar el volumen montado
// ./web/wwwroot).
// ---------------------------------------------------------------
const CONFIG = {
  REGISTRO_CLIENTE_URL: "http://localhost:5001",
  REGISTRO_PAGO_URL: "http://localhost:5002",
  CONSULTA_CLIENTES_URL: "http://localhost:5003",
  CONSULTA_PAGOS_URL: "http://localhost:5004"
};
