# DockerWebinar — Laboratorio híbrido Windows + Linux Containers

## Estructura del proyecto

```
dockerwebinar/
├── docker-compose.yml         <- Motor Windows: web + 4 microservicios independientes
├── web/
│   ├── Dockerfile              <- Imagen IIS (Windows)
│   └── wwwroot/
│       ├── index.html
│       ├── css/style.css
│       └── js/{config.js, app.js}
├── registro-cliente/            <- POST /registrocliente   (.NET 8, Windows container)
│   ├── Dockerfile
│   ├── RegistroCliente.csproj
│   └── Program.cs
├── registro-pago/               <- POST /registropago      (.NET 8, Windows container)
│   ├── Dockerfile
│   ├── RegistroPago.csproj
│   └── Program.cs
├── consulta-clientes/           <- GET  /consultaclientes  (.NET 8, Windows container)
│   ├── Dockerfile
│   ├── ConsultaClientes.csproj
│   └── Program.cs
├── consulta-pagos/              <- GET  /consultapagos     (.NET 8, Windows container)
│   ├── Dockerfile
│   ├── ConsultaPagos.csproj
│   └── Program.cs
└── database/init/sql/init-dockerwebinar.sql   <- Crea la base DockerWebinar y las tablas Clientes/Pagos
```

Cada microservicio es una carpeta autocontenida e idéntica en forma: un `Dockerfile`,
un `.csproj` y un único `Program.cs` con **un solo endpoint de negocio** (más `/health`
y `/health/db` para diagnóstico). Puedes reemplazar cualquiera sin tocar los demás:
basta con cambiar el contenido de su carpeta (o apuntar `build.context` a otra ruta en
`docker-compose.yml`) y correr `docker compose up --build -d <nombre-del-servicio>`.

## SQL Server (Linux Container en WSL2)

Este SQL Server (Linux/Ubuntu container en WSL2) se queda exactamente igual a como ya
lo tenías corriendo; no se toca su `docker-compose`, porque los dos motores Docker
(Windows y Linux/WSL2) son completamente independientes:

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: sqlserver
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_PID: "Developer"
      MSSQL_SA_PASSWORD: "DockerDemo2026!"
    ports:
      - "1433:1433"
    volumes:
      - sql_data:/var/opt/mssql
volumes:
  sql_data:
```

## Requisito de host (una sola vez): reenvío de puerto Windows → WSL2

Antes de levantar los microservicios, el host Windows necesita reenviar el puerto 1433
a **todas** las interfaces, porque WSL2 solo lo reenvía por defecto a `127.0.0.1`
(loopback) — y un contenedor Windows no puede usar el `localhost` del host. Sin este
paso, los cuatro microservicios fallan con `SqlException ... The wait operation timed
out.` aunque SQL Server esté corriendo bien.

En PowerShell **como administrador**, una sola vez (persiste entre reinicios del host,
incluso si cambia la IP interna de WSL2, porque ahora se usa `127.0.0.1`):

```powershell
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=1433 connectaddress=127.0.0.1 connectport=1433
```

Verificar que quedó activo:

```powershell
netsh interface portproxy show v4tov4
```

Con esto, `host.docker.internal:1433` (usado en `docker-compose.yml`) queda accesible
desde los contenedores Windows.

Si alguna vez lo necesitas quitar:

```powershell
netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=1433
```

## Pasos para levantar el laboratorio

1. **Prepara la base de datos** (una sola vez). Desde SSMS, conectado a `localhost,1433`
   (o la IP de tu WSL2) con el usuario `sa`, ejecuta el script
   `database/init/sql/init-dockerwebinar.sql`. Esto crea la base `DockerWebinar`, las
   tablas `Clientes` y `Pagos`, y algunos clientes de ejemplo.

2. **Verifica que Docker Desktop esté en modo "Switch to Windows containers..."** (clic
   derecho sobre el ícono de Docker Desktop en la bandeja del sistema).

3. **Levanta los cinco servicios Windows** (web + los 4 microservicios):

   ```powershell
   cd dockerwebinar
   docker compose up --build -d
   ```

4. **Abre el sitio**: [http://localhost:8000](http://localhost:8000)

## Endpoints de cada microservicio

| Microservicio | Puerto | Endpoint | Verbo |
|---|---|---|---|
| `registro-cliente` | 5001 | `/registrocliente` | POST |
| `registro-pago` | 5002 | `/registropago` | POST |
| `consulta-clientes` | 5003 | `/consultaclientes` | GET |
| `consulta-pagos` | 5004 | `/consultapagos` | GET |

Todos exponen además `/health` (proceso vivo) y `/health/db` (conexión real a SQL
Server, útil para depurar).

## Cómo evidenciar los microservicios independientes en el webinario

Con los cinco contenedores arriba, detén solo uno y muestra en vivo que el resto del
sitio sigue funcionando — ahora con **cuatro** puntos de falla independientes en vez
de dos:

```powershell
# Apaga solo el registro de clientes
docker compose stop registro-cliente
# En la página: solo el formulario "Registrar cliente" muestra la alerta roja;
# registrar pagos y ambas consultas siguen funcionando.
docker compose start registro-cliente
```

```powershell
# Apaga solo la consulta de pagos
docker compose stop consulta-pagos
# En la página: solo la tabla "Pagos registrados" muestra la alerta roja;
# los otros tres microservicios siguen funcionando.
docker compose start consulta-pagos
```

Repite lo mismo con `registro-pago` y `consulta-clientes` para mostrar los cuatro
casos. El badge de estado de cada microservicio en la parte superior de la página
(verde/rojo) se actualiza automáticamente cada 15 segundos, y también al presionar
"Reintentar todo".

## Cómo reemplazar un microservicio

Cada carpeta (`registro-cliente/`, `registro-pago/`, `consulta-clientes/`,
`consulta-pagos/`) es independiente y sigue la misma forma: un `Dockerfile`, un
`.csproj` y un `Program.cs`. Para reemplazar uno:

1. Sustituye el contenido de esa carpeta por tu propia implementación (puede ser
   otro lenguaje, otro framework, o simplemente otra versión del mismo código),
   siempre que siga exponiendo el mismo endpoint y puerto interno (`8080`), o que
   ajustes el mapeo de puertos en `docker-compose.yml` si cambia.
2. Reconstruye solo ese servicio:
   ```powershell
   docker compose up --build -d <nombre-del-servicio>
   ```
3. Los otros tres microservicios y el sitio web no se ven afectados.

## Notas técnicas

- Los cuatro microservicios están en imágenes **Windows** (`nanoserver-ltsc2022`) para
  vivir en el mismo `docker-compose.yml` del motor Windows, junto con IIS — evidenciando
  microservicios separados, cada uno con su propio ciclo de vida, dentro del mismo
  stack de contenedores Windows.
- Se habilitó CORS abierto (`AllowAnyOrigin`) en los cuatro microservicios únicamente
  para fines de demostración del webinario; en un entorno productivo se restringiría al
  origen real del sitio.
- La cadena de conexión a SQL Server se arma con `Microsoft.Data.SqlClient` y usa
  `TrustServerCertificate=True` porque el SQL Server del contenedor Linux usa un
  certificado autofirmado.
- Ningún `.csproj` debe llevar `InvariantGlobalization=true`: `Microsoft.Data.SqlClient`
  necesita datos de cultura reales; con el modo invariante activado falla con
  `NotSupportedException: Globalization Invariant Mode is not supported`.
- Si necesitas cambiar el host o la base de datos, solo edita las variables de entorno
  `SQL_SERVER` / `SQL_DATABASE` en `docker-compose.yml` y recrea los microservicios
  afectados (`docker compose up -d --force-recreate <servicio>`), sin tocar la página
  web ni el código de las APIs.
