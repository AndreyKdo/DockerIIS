# DockerWebinar — Laboratorio híbrido Windows + Linux Containers

Laboratorio de demostración para el **webinar dirigido a desarrolladores** (jueves 17 de
setiembre de 2026, 10:00 a.m.). El objetivo no es la aplicación en sí —un CRUD mínimo de
clientes y pagos—, sino usarla para mostrar, en vivo y sobre un caso real,
los conceptos que un desarrollador necesita para empezar a trabajar con Docker.

## Propósito del webinario

**Audiencia:** desarrolladores (con o sin experiencia previa en contenedores).

Lo que este laboratorio permite demostrar:

| Concepto | Dónde se ve en este proyecto |
|---|---|
| Imagen vs. contenedor | Cada `Dockerfile` (build) frente a cada contenedor corriendo (`docker ps`) |
| Dockerfile y builds multi-etapa | `registro-cliente/Dockerfile` (SDK → runtime), `ConsultaNodeJS/Dockerfile` |
| Orquestación básica | `docker-compose.yml` levantando cinco servicios de un solo comando |
| Microservicios independientes | Apagar uno solo y ver que los otros tres siguen respondiendo |
| Windows y Linux containers en la misma máquina | IIS + .NET en el motor Windows; Node.js en el motor Linux; SQL Server en la VM |
| Cambio de motor en Docker Desktop | *Switch to Linux/Windows containers…* y sus consecuencias sobre lo que ya está corriendo |
| Configuración por variables de entorno | `SQL_SERVER`, `SQL_DATABASE`, `SQL_USER`, `SQL_PASSWORD` |
| Volúmenes | `./web/wwwroot` montado dentro del contenedor IIS (se edita sin reconstruir) |
| Redes y comunicación entre motores Docker | Red `dockerlab` + red NAT de VMware (VMnet8) hacia la VM Ubuntu |
| Portabilidad entre lenguajes | El mismo endpoint reimplementado en Node.js (`ConsultaNodeJS/`) |
| Publicación de imágenes | `docker tag` / `docker push` a un registro (ver *Comandos útiles*) |
| Troubleshooting real | `/health`, `/health/db`, `docker logs`, reintentos de conexión |

**Mensaje central del webinar:** el contenedor no es "una VM más liviana", es una unidad de
despliegue independiente. Por eso la demo consiste en apagar piezas en vivo y mostrar que
el resto del sistema sigue de pie.

## Arquitectura del laboratorio

```
   Host Windows — Docker Desktop, MOTOR WINDOWS (modo Windows containers)
   ┌───────────────────────────────────────────────────────────────────────┐
   │  web-iis (IIS)          registro-cliente  :5001                       │
   │  :8000 → :80            registro-pago     :5002                       │
   │  volumen ./web/wwwroot  consulta-clientes :5003                       │
   │                         consulta-pagos    :5004                       │
   └───────────────────────────────────────────────────────────────────────┘

   Host Windows — Docker Desktop, MOTOR LINUX (modo Linux containers)
   ┌───────────────────────────────────────────────────────────────────────┐
   │  consulta-clientes-node (node:20-alpine)  :5005 → :8080               │
   │  API + su propia vista web servidas por Express                       │
   └───────────────────────────────────────────────────────────────────────┘
                                   │
                                   │  TCP 1433 (red NAT de VMware, VMnet8)
                                   ▼
                VM Ubuntu (VMware Workstation) — motor Docker Linux
   ┌───────────────────────────────────────────────────────────────────────┐
   │  sqlserver (mssql/server:2022-latest)   :1433                         │
   └───────────────────────────────────────────────────────────────────────┘
```

Son **tres motores Docker distintos** en juego: los dos de Docker Desktop en el host
(Windows y Linux, que no corren al mismo tiempo — ver más abajo) y el de la VM Ubuntu.
Ninguno ve los contenedores, imágenes ni redes de los otros; se comunican solo por TCP.

Los dos motores Docker (Windows y Linux) son independientes: cada uno tiene su propio
`docker-compose.yml` y no se ven entre sí; se comunican por red TCP normal.

## Requisitos previos

- **Docker Desktop** en Windows, con la opción *Switch to Windows containers…* activada.
- **VMware Workstation** con una VM Ubuntu (adaptador de red en modo **NAT**) y Docker
  instalado dentro de ella, para el contenedor de SQL Server.
- ** A preferencia: SQL Server Management Studio (SSMS)** o `sqlcmd` para ejecutar el script de inicialización.
- **Visual Studio Code** (IDE usado en el laboratorio) — opcional.
- No hace falta instalar el **SDK de .NET 8** ni **Node.js** en el host: ambos viven dentro
  de las imágenes de build. Instalarlos localmente solo es útil para depurar fuera de Docker.
- Puertos libres en el host: `8000`, `5001`, `5002`, `5003`, `5004` y `5005`.

## Estructura del proyecto

```
dockerwebinar/
├── docker-compose.yml         <- Motor Windows: web + 4 microservicios independientes
├── web/
│   ├── Dockerfile              <- Imagen IIS (Windows)
│   └── wwwroot/                <- Se monta como volumen: editar no requiere rebuild
│       ├── index.html
│       ├── css/style.css
│       ├── js/{config.js, app.js}
│       └── mobile/index.html   <- Página estática de prueba (no forma parte de la demo)
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
├── ConsultaNodeJS/              <- GET /consultaclientes en Node.js    (Linux container, puerto 5005)
│   ├── docker-compose.yml       <- Compose propio del motor Linux
│   ├── Dockerfile               <- node:20-alpine, build multi-etapa
│   ├── package.json             <- express + mssql
│   ├── index.js                 <- GET /consultaclientes + /health + /health/db
│   └── public/                  <- Vista propia servida por el mismo contenedor
│       ├── index.html
│       ├── css/style.css
│       └── js/{config.js, app.js}
└── database/init/
    ├── sql/init-dockerwebinar.sql  <- Script idempotente: base + tablas + datos de ejemplo
    ├── 01-create-db.sql            <- Versión paso a paso: crea la base
    ├── 02-schema.sql               <- Versión paso a paso: crea Clientes y Pagos
    └── 03-data.sql                 <- Versión paso a paso: inserta datos de ejemplo
```

Cada microservicio es una carpeta autocontenida e idéntica en forma: un `Dockerfile`,
un `.csproj` y un único `Program.cs` con **un solo endpoint de negocio** (más `/health`
y `/health/db` para diagnóstico). Puedes reemplazar cualquiera sin tocar los demás:
basta con cambiar el contenido de su carpeta (o apuntar `build.context` a otra ruta en
`docker-compose.yml`) y correr `docker compose up --build -d <nombre-del-servicio>`.

> **Sobre los scripts SQL:** `init-dockerwebinar.sql` hace todo de una sola vez y es
> idempotente (se puede correr varias veces sin error). Los archivos `01`, `02` y `03`
> hacen exactamente lo mismo separados por etapa, basta con ejecutar **uno de los dos caminos**.

## SQL Server (Linux Container en VMware Workstation)

Este SQL Server (Linux/Ubuntu container en VMware Workstation) se queda exactamente igual a como ya
lo tenías corriendo; no se toca su `docker-compose`, porque los dos motores Docker
(el de Windows en el host y el de Linux dentro de la VM) son completamente independientes:

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
    restart: always #OPCIONAL para que levante automáticamente al inicializar la VM
    volumes:
      - sql_data:/var/opt/mssql
volumes:
  sql_data:
```

El volumen `sql_data` es la pieza que conviene resaltar: los datos sobreviven
a un `docker rm` del contenedor, porque no viven dentro de él.

## Requisito de host: Configuración de red NAT (Windows → Ubuntu VM en VMware Workstation)

Para que los **contenedores Windows** (ejecutados en la máquina local/host) puedan
comunicarse con el **contenedor Linux de SQL Server** alojado dentro de la VM Ubuntu, la
máquina virtual usa el adaptador de red en modo **NAT** de VMware Workstation, que es la
opción por defecto y la que se utiliza en este laboratorio.

### ¿Por qué funciona con NAT?

Al usar NAT, VMware coloca a la VM en una red virtual privada (la subred de **VMnet8**) y,
al mismo tiempo, crea en el host un adaptador virtual **VMware Network Adapter VMnet8** con
una IP dentro de esa misma subred. Es decir: el host es un miembro más de la red NAT, así
que puede alcanzar a la VM directamente por su IP (en este laboratorio, `192.168.6.129`)
sin ninguna configuración extra. Los contenedores Windows, a su vez, salen a la red a
través del host, de modo que ese mismo camino les sirve para llegar al puerto `1433` del
contenedor de SQL Server.

Lo que NAT no permite es lo contrario: la VM **no** es visible desde otros equipos de la
LAN. Para este laboratorio eso no importa (todo corre en la misma máquina) y de hecho es
una ventaja para el webinar: **no depende del router ni de la red de la sala**, así que
funciona igual con cable, con wifi de invitados o sin red física.

### Pasos de configuración

1. Abrir VMware Workstation y seleccionar la máquina virtual Ubuntu.
2. Ir a **VM > Settings (Configuración) > Network Adapter (Adaptador de red)**.
3. Seleccionar la opción **NAT: Used to share the host's IP address**.
4. Iniciar la VM y anotar la IP que le asignó el DHCP de VMware (normalmente en la
   interfaz `ens33`):
```bash
   ip a
```
5. En el host, confirmar que el adaptador VMnet8 está activo y en la misma subred:
```powershell
   ipconfig
```
6. Confirmar conectividad desde el host hacia la VM:
```powershell
   ping <IP_DE_LA_VM_UBUNTU>
   Test-NetConnection <IP_DE_LA_VM_UBUNTU> -Port 1433
```
7. Permitir el puerto de SQL Server en el firewall de Ubuntu, si está activo:
```bash
   sudo ufw allow 1433/tcp
```
8. Colocar esa IP en la variable `SQL_SERVER` de los cuatro microservicios en
   `docker-compose.yml` (no `localhost`, que dentro del contenedor apunta al contenedor
   mismo) y recrearlos.
9. Verificar la ruta completa desde un contenedor Windows ya levantado:
```powershell
   docker exec -it registro-cliente powershell -Command "Test-NetConnection <IP_DE_LA_VM_UBUNTU> -Port 1433"
```

> **Importante antes de la demo:** la IP de la VM la asigna el DHCP de VMware y puede cambiar. 
> Verificar la IP real con `ip a` y actualizar `SQL_SERVER` en el compose antes de levantar los servicios es el
> paso que más veces rompe el laboratorio si se olvida.

### Cómo fijar la IP de la VM

Para no depender de lo que entregue el DHCP, hay dos caminos:

- **Reserva DHCP en VMware:** abrir el **Virtual Network Editor** como administrador,
  seleccionar **VMnet8**, entrar a **DHCP Settings…** y asociar la MAC de la VM a una IP
  fija dentro del rango NAT.
- **IP estática en Ubuntu:** configurarla en netplan (`/etc/netplan/*.yaml`) dentro del
  rango de VMnet8, usando como gateway la IP `.2` de esa subred (la puerta NAT de VMware).

### Opcional: reenvío de puertos NAT (usar la IP del host)

Si se prefiere no depender de la IP de la VM, VMware permite publicar el puerto en el host:
**Virtual Network Editor > VMnet8 > NAT Settings… > Port Forwardings > Add**, mapeando el
puerto `1433` del host al `1433` de la IP de la VM. Con eso, los microservicios pueden
apuntar `SQL_SERVER` a `host.docker.internal` (el valor por defecto en el código si no se
define la variable) y SSMS puede conectarse a `localhost,1433`.

Es una alternativa cómoda, pero agrega una capa más que explicar; para nuestro objetivo
es más claro apuntar directamente a la IP de la VM.

### Solución de problemas: la VM no obtiene IP o se queda atascada al iniciar (red)

Si al encender la VM esta se queda "colgada" durante la inicialización de red, o arranca
pero `ip a` no muestra ninguna IP en `ens33`:

1. Asegúrate de que la VM esté **detenida** (Stopped), no solo suspendida.
2. Abre el **VMware Virtual Network Editor** (Inicio > buscar "Virtual Network Editor").
3. Ejecútalo **como administrador**. Esto es importante: si no lo ejecutas como
   administrador, los adaptadores virtuales aparecen en gris y no se pueden modificar.
   También puedes hacer clic en el botón inferior **"Change Settings"** para elevar
   permisos dentro de la misma ventana.
4. Verifica que **VMnet8** exista, esté marcado como tipo **NAT** y tenga habilitada la
   opción **"Use local DHCP service to distribute IP addresses to VMs"**.
5. Si la configuración quedó inconsistente (por ejemplo, tras migrar de VMware Player a
   Workstation), usa **"Restore Defaults"** para regenerar las redes virtuales, y vuelve a
   confirmar que el adaptador de la VM sigue en **NAT**.
6. En el host, revisa en **services.msc** que estén corriendo **VMware NAT Service** y
   **VMware DHCP Service**, y en **Conexiones de red** que el adaptador
   **VMware Network Adapter VMnet8** no esté deshabilitado.
7. Haz clic en **Apply** y **OK**, y vuelve a iniciar la VM; debería arrancar normalmente y
   obtener IP dentro de la subred NAT.


### Resultado esperado

Con esta configuración, los contenedores Windows (IIS + microservicios .NET) podrán conectarse directamente a la IP NAT de la VM Ubuntu, permitiendo que la arquitectura híbrida del laboratorio (Windows Containers ↔ SQL Server en Linux Container) funcione correctamente durante las pruebas y la presentación, sin depender de la red física.

## Pasos para levantar el laboratorio

1. **Prepara la base de datos** (una sola vez). Desde SSMS, conectado a `localhost,1433`
   (o la IP de tu VM) con el usuario `sa`, ejecuta el script
   `database/init/sql/init-dockerwebinar.sql`. Esto crea la base `DockerWebinar`, las
   tablas `Clientes` y `Pagos`, y algunos clientes de ejemplo.

2. **Verifica que Docker Desktop esté en modo "Switch to Windows containers..."** (clic
   derecho sobre el ícono de Docker Desktop en la bandeja del sistema).

3. **Ajusta `SQL_SERVER`** en `docker-compose.yml` con la IP real de la VM Ubuntu (los
   cuatro microservicios comparten el mismo valor).

4. **Levanta los cinco servicios Windows** (web + los 4 microservicios):

   ```powershell
   cd dockerwebinar
   docker compose up --build -d
   ```

5. **Abre el sitio**: [http://localhost:8000](http://localhost:8000)

6. **Verifica los badges de estado** en la parte superior de la página: los cuatro deben
   quedar en verde. Si alguno queda rojo, consulta `http://localhost:<puerto>/health/db`
   para ver el error real de conexión a SQL Server.

7. **Cambia Docker Desktop al motor Linux** (clic derecho sobre el ícono en la bandeja >
   *Switch to Linux containers…*) y espera a que el motor termine de reiniciar.
   **los contenedores Windows no se pueden gestionar al hacer el cambio pero se siguen ejecutando**.

8. **Levanta el microservicio Node.js**, que corre sobre ese motor Linux:

   ```powershell
   cd ConsultaNodeJS
   docker compose up --build -d
   ```

9. **Abre su vista propia**: [http://localhost:5005](http://localhost:5005). No necesita IIS:
   el mismo contenedor Express sirve la API y su interfaz.

10. **Vuelve al motor Windows** (*Switch to Windows containers…*) cuando quieras gestionar el
    sitio de IIS. Como los cinco servicios tienen `restart: unless-stopped`, vuelven a
    levantarse solos al reiniciar ese motor.

### Cómo bajar el laboratorio

```powershell
docker compose down            # detiene y elimina los contenedores
docker compose down --rmi all  # además elimina las imágenes construidas
```

## Endpoints de cada microservicio

| Microservicio | Puerto | Endpoint | Verbo |
|---|---|---|---|
| `registro-cliente` | 5001 | `/registrocliente` | POST |
| `registro-pago` | 5002 | `/registropago` | POST |
| `consulta-clientes` | 5003 | `/consultaclientes` | GET |
| `consulta-pagos` | 5004 | `/consultapagos` | GET |
| `consulta-clientes-node` *(motor Linux)* | 5005 | `/consultaclientes` | GET |

Todos exponen además `/health` (proceso vivo) y `/health/db` (conexión real a SQL
Server, útil para depurar).


## Variables de entorno

Se definen por servicio en `docker-compose.yml`. Si no se envían, el código usa los
valores por defecto que aparecen en la última columna:

| Variable | Para qué sirve | Valor por defecto en el código |
|---|---|---|
| `SQL_SERVER` | Host o IP del SQL Server (la VM Ubuntu) | `host.docker.internal` |
| `SQL_DATABASE` | Nombre de la base | `DockerWebinar` |
| `SQL_USER` | Usuario de SQL Server | `sa` |
| `SQL_PASSWORD` | Contraseña del usuario | `DockerDemo2026!` |

Cambiar cualquiera de ellas no requiere reconstruir la imagen, solo recrear el contenedor:

```powershell
docker compose up -d --force-recreate <servicio>
```

Este es un buen momento para explicar por qué la configuración va en variables
de entorno y no dentro de la imagen: **la misma imagen sirve para desarrollo, pruebas y
producción**; lo único que cambia es el entorno donde corre.

## Configuración del front-end (`web/wwwroot/js/config.js`)

El sitio IIS es HTML/CSS/JS estático y llama a los cuatro microservicios desde el navegador,
usando las URLs de `config.js`:

```javascript
const CONFIG = {
  REGISTRO_CLIENTE_URL: "http://localhost:5001",
  REGISTRO_PAGO_URL: "http://localhost:5002",
  CONSULTA_CLIENTES_URL: "http://localhost:5003",
  CONSULTA_PAGOS_URL: "http://localhost:5004"
};
```

Como `./web/wwwroot` está montado por volumen, editar este archivo y refrescar el navegador
basta: **no hay que reconstruir ni reiniciar el contenedor de IIS**. Es la demostración más
rápida de qué hace un volumen, y sirve también para apuntar el sitio al microservicio Node.js
(cambiando `CONSULTA_CLIENTES_URL` a `http://localhost:5005`) sin tocar nada más. Ojo: para
ver ambos al mismo tiempo el contenedor Node.js tendría que estar corriendo en la VM Ubuntu
—apuntando a `http://<IP_DE_LA_VM>:5005`—, porque mientras Docker Desktop está en modo Linux
el sitio de IIS no está arriba.

Como las llamadas salen del navegador y no del contenedor, las URLs son las que ve **el
host** (`localhost:500x`), no los nombres de servicio de la red `dockerlab`.

## Cómo evidenciar los microservicios independientes

Con los cinco contenedores arriba, detén solo uno y así se muestra que el resto del
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

## El microservicio en Node.js (`ConsultaNodeJS/`) — segundo motor en acción

`ConsultaNodeJS/` no es un extra: es la pieza que permite **evidenciar Docker corriendo en
los dos motores**, Windows y Linux, sobre la misma máquina y contra la misma base de datos.
Es el mismo endpoint `GET /consultaclientes`, con el mismo contrato JSON, reimplementado en
Node.js (Express + `mssql`) sobre una imagen `node:20-alpine`. Demuestra dos cosas a la vez:
que lo que define al microservicio es su contrato y no su lenguaje, y que un mismo host
puede ejecutar contenedores de dos sistemas operativos distintos.

**Salvedad importante:** requiere **cambiar Docker Desktop al motor Linux**
(*Switch to Linux containers…*), lo cual no permite gestionar (ni ver) los contenedores Windows.
Para visualizar los contenedores Windows, debes hacer click en (*Switch to Windows containers…*)


```powershell
# Con Docker Desktop ya en modo Linux containers
cd ConsultaNodeJS
docker compose up --build -d
docker ps                      # solo aparece el contenedor Linux: el motor cambió
```

Diferencias frente a la versión .NET, que valen la pena señalar en vivo:

- Corre como **contenedor Linux** en el motor Linux de Docker Desktop, con su propio
  `docker-compose.yml` y su propio puerto (`5005:8080`).
- Trae su **propia vista web** (`public/`) servida por el mismo contenedor Express, así que
  no depende de IIS —lo cual es indispensable aquí, porque mientras el motor Linux está
  activo el sitio de IIS no está corriendo—. Su `config.js` usa `BASE_URL: ""` (mismo
  origen, rutas relativas), mientras el sitio IIS necesita URLs absolutas porque los
  servicios están en otros puertos.
- La imagen final es de decenas de MB frente a los varios GB de `windowsservercore-ltsc2022`:
  la comparación es la forma más gráfica de explicar por qué el tamaño de la imagen base
  importa. Como `docker images` solo muestra las imágenes del motor activo, conviene capturar la salida de
  cada motor por separado y ponerlas lado a lado en la diapositiva.
- El build también es multi-etapa, pero tarda segundos en vez de minutos: buen momento para
  hablar del costo real de las imágenes base de Windows.
- La lógica es equivalente línea por línea: mismos health checks, mismo esquema de
  reintentos (3 intentos con 700 ms de espera), mismas variables de entorno y mismo CORS
  abierto.
- Los clientes que se registren desde el sitio IIS aparecen en la vista Node.js y al revés:
  ambos motores están hablando con el **mismo** contenedor de SQL Server en la VM Ubuntu.

## Comandos Docker útiles

```powershell
# Ciclo de vida
docker compose up --build -d          # construir y levantar todo
docker compose ps                     # estado de los servicios del compose
docker compose logs -f registro-pago  # seguir los logs de un servicio
docker compose restart consulta-pagos

# Inspección
docker ps -a                          # contenedores (incluye detenidos)
docker images                         # imágenes locales y su tamaño
docker inspect registro-cliente       # configuración completa en JSON
docker stats                          # consumo de CPU/memoria en vivo
docker exec -it registro-cliente powershell   # entrar a un contenedor Windows
docker exec -it consulta-clientes-node sh     # entrar a un contenedor Linux

# Cambiar de motor (también por clic derecho en el ícono de Docker Desktop)
& "C:\\Program Files\\Docker\\Docker\\DockerCli.exe" -SwitchDaemon
docker info --format "{{.OSType}}"    # confirma en qué motor estás: windows o linux
Ver motores disponibles: docker desktop engine ls

#PRECAUCIÓN CON USAR LOS SIGUIENTES COMANDOS: la CLI realiza un cambio forzado y abrupto que a veces "congela" o detiene los contenedores de fondo
Cambiar al motor de Windows: docker desktop engine use windows
Cambiar al motor de Linux: docker desktop engine use linux


# Limpieza (útil entre ensayos)
docker compose down
docker image prune -f
docker system df                      # cuánto espacio está ocupando Docker
```

### Publicar una imagen en un registro

```powershell
docker login
docker tag dockerwebinar-consulta-clientes <usuario>/consulta-clientes:1.0
docker push <usuario>/consulta-clientes:1.0

# Y en otra máquina, sin código fuente:
docker run -d -p 5003:8080 -e SQL_SERVER=<ip> <usuario>/consulta-clientes:1.0
```

Si no hay internet en la sala, la alternativa offline para mostrar portabilidad es:

```powershell
docker save <usuario>/consulta-clientes:1.0 -o consulta-clientes.tar
docker load -i consulta-clientes.tar
```

## Solución de problemas frecuentes

| Síntoma | Causa probable | Qué revisar |
|---|---|---|
| Badge rojo y `/health` no responde | El contenedor no está arriba | `docker compose ps`, `docker compose logs <servicio>` |
| `/health` responde pero `/health/db` falla | Problema de red hacia SQL Server | IP en `SQL_SERVER`, VM encendida, adaptador en NAT, `ping <IP>`, `ufw` en Ubuntu |
| `Login failed for user 'sa'` | Credenciales o base inexistente | Contraseña del compose vs. la del contenedor SQL; correr `init-dockerwebinar.sql` |
| `NotSupportedException: Globalization Invariant Mode` | `InvariantGlobalization=true` en el `.csproj` | Quitar esa propiedad y reconstruir |
| Error de CORS en la consola del navegador | El servicio está caído (no llegó a responder con las cabeceras) | Levantar el servicio; el CORS está abierto en los cuatro |
| El build de Windows tarda muchísimo la primera vez | Descarga de `windowsservercore-ltsc2022` (varios GB) | Hacer `docker compose build` **antes** del webinar |
| `docker compose up` falla con "no matching manifest" | El compose del stack Windows se corrió con Docker Desktop en modo Linux (o el de Node.js en modo Windows) | Cambiar de motor con *Switch to…* según el stack que se quiera levantar |
| "Desaparecieron mis imágenes/contenedores" | Cada motor tiene su propio inventario | Verificar en qué modo está Docker Desktop; `docker ps` solo muestra el motor activo |
| El sitio `:8000` no responde después de levantar Node.js | Se cambió al motor Linux y los contenedores Windows se detuvieron | Volver a *Switch to Windows containers…*; arrancan solos por `restart: unless-stopped` |
| Cambios en el HTML no se ven | Caché del navegador (no el volumen) | Recargar con `Ctrl+F5` |
| Puerto ocupado al levantar | Otro proceso usa 8000/500x | `netstat -ano \| findstr :8000` y cambiar el mapeo en el compose |

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
- Docker Desktop mantiene **un solo motor activo a la vez**; los contenedores del otro motor
  no se pueden gestionar, no se eliminan y (si se detiene mediante el ejecutable DockerCli.exe) los contenedores se siguen ejecutando. El laboratorio usa esa característica a propósito para mostrar ejecución en ambos motores desde el mismo host.
- Todos los `Dockerfile` usan **build multi-etapa** (SDK/`node` para compilar, runtime para
  ejecutar): la imagen final no arrastra el compilador ni el código fuente.
- La apertura de conexión reintenta **3 veces con 700 ms de espera** antes de fallar, porque
  el handshake inicial de esta ruta de red puede perderse de forma intermitente. Es una
  decisión deliberada frente a subir el timeout indefinidamente.
- Los servicios comparten la red `dockerlab` del compose, lo que les permitiría llamarse
  entre sí por nombre de servicio; en este laboratorio no lo hacen, porque el orquestador
  de las llamadas es el navegador.
- `web/wwwroot/mobile/index.html` es una página estática de prueba que quedó del andamiaje
  inicial; no participa en la demo y se puede eliminar sin afectar nada.

## Advertencias (esto es un laboratorio, no producción)

- Las credenciales de `sa` están **en texto plano** en los archivos `docker-compose.yml`.
  En un entorno real irían en Docker secrets, variables del orquestador o un vault, y el
  compose no se versionaría con ellas.
- `sa` es una cuenta administrativa: en producción se usaría un usuario con permisos
  mínimos sobre la base.
- CORS abierto, `Encrypt=False` y `TrustServerCertificate=True` son concesiones de demo.
- No hay `HEALTHCHECK` en los `Dockerfile` ni límites de recursos; los health checks los
  hace el front-end cada 15 segundos, que es lo que se quiere mostrar visualmente.

## Créditos

Laboratorio y sitio desarrollados por **Andrey F. Picado Arias**.
Repositorio del laboratorio:
[github.com/AndreyKdo/DockerIIS](https://github.com/AndreyKdo/DockerIIS)
