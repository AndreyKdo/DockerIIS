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

## SQL Server (Linux Container en VMware Workstation)

Este SQL Server (Linux/Ubuntu container en VMware Workstation) se queda exactamente igual a como ya
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

## Requisito de host: Configuración de red Bridged (Windows → Ubuntu VM en VMware Workstation)

Para que los **contenedores Windows** (ejecutados en la máquina local/host) puedan comunicarse con el **contenedor Linux de SQL Server** alojado dentro de la VM Ubuntu, es necesario configurar el adaptador de red de la máquina virtual en modo **Bridged (Puente)** en VMware Workstation.

### ¿Por qué es necesario?

Por defecto, VMware Workstation utiliza NAT para las máquinas virtuales, lo cual aísla a la VM detrás de una red virtual interna y le asigna una IP que no es directamente visible ni enrutable desde el host ni desde los contenedores Windows que corren sobre él. Al usar el modo **Bridged**, la VM Ubuntu obtiene una IP dentro de la misma red local (LAN) que la máquina física, permitiendo que el contenedor de SQL Server sea alcanzable como si fuera un equipo más de la red.

### Pasos de configuración

1. Abrir VMware Workstation y seleccionar la máquina virtual Ubuntu.
2. Ir a **VM > Settings (Configuración) > Network Adapter (Adaptador de red)**.
3. Seleccionar la opción **Bridged: Connected directly to the physical network**.
4. (Opcional pero recomendado) Marcar **Replicate physical network connection state** para que la VM siga la conexión del host.
5. Iniciar/reiniciar la VM y verificar que obtuvo una IP dentro del mismo rango de la red local, con:
```bash
   ip a
```
6. Confirmar conectividad desde el host hacia la VM:
```powershell
   ping <IP_DE_LA_VM_UBUNTU>
```
7. Verificar que el puerto de SQL Server (por defecto **1433**) esté expuesto y accesible desde el contenedor Windows, ajustando la cadena de conexión de los microservicios .NET para apuntar a la IP de la VM en lugar de `localhost`.

### Solución de problemas: la VM se queda atascada al iniciar (búsqueda de red)

Si al encender la VM esta se queda "colgada" o atascada durante la fase de búsqueda/inicialización de red, sigue estos pasos:

1. Asegúrate de que la VM esté **detenida** (Stopped), no solo suspendida.
2. Abre el **VMware Virtual Network Editor** (Inicio > buscar "Virtual Network Editor").
3. Ejecútalo **como administrador**. Esto es importante: si no lo ejecutas como administrador, **VMnet0 no será visible** en la lista. También puedes hacer clic en el botón inferior **"Change Settings"** para elevar permisos dentro de la misma ventana.
4. Selecciona **VMnet0** en la lista y haz clic en **"Automatic Settings..."**.
5. Se mostrará un listado de adaptadores de red disponibles. **Deselecciona todos excepto la tarjeta de red física** que estás usando para salir a internet/LAN.
   > Nota: en algunas instalaciones (por ejemplo, tras migrar de VMware Player a Workstation), todos los adaptadores quedan marcados por defecto, lo cual puede causar conflictos y provocar que la VM se atasque buscando la interfaz correcta.
6. Haz clic en **Apply** y **OK** para guardar los cambios.
7. Vuelve a iniciar la VM; debería arrancar normalmente y obtener IP en modo Bridged sin quedarse atascada.


### Resultado esperado

Con esta configuración, los contenedores Windows (IIS + microservicios .NET) podrán resolver y conectarse directamente a la IP de la VM Ubuntu, permitiendo que la arquitectura híbrida del laboratorio (Windows Containers ↔ SQL Server en Linux Container) funcione correctamente durante las pruebas y la presentación.

## Pasos para levantar el laboratorio

1. **Prepara la base de datos** (una sola vez). Desde SSMS, conectado a `localhost,1433`
   (o la IP de tu VM) con el usuario `sa`, ejecuta el script
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

- Los cuatro microservicios están en imágenes **Windows** (`windowsservercore-ltsc2022`) para
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
