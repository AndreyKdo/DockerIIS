// La vista se sirve desde el mismo contenedor Express que expone la API,
// asi que se usan rutas relativas (mismo origen). Si en algun momento la
// vista se separa a otro host/puerto, cambia BASE_URL aqui.
const CONFIG = {
  BASE_URL: "",
};
