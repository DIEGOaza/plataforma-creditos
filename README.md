# PlataformaCreditos

Plataforma de solicitudes de crédito desarrollada con ASP.NET Core 10, SQLite, Redis, SignalR y RabbitMQ/CloudAMQP.

## Requisitos

- .NET 10 SDK.
- Docker Desktop para ejecutar Redis local en un contenedor.
- Una instancia de Redis accesible para la caché y las sesiones.
- Una cola RabbitMQ/CloudAMQP con soporte AMQPS para publicar `SolicitudRegistrada`.
- Git y una cuenta de GitHub para desplegar desde Render.

## Estructura del proyecto

```text
.
├── Dockerfile
├── .dockerignore
├── PlataformaCreditos/
│   ├── PlataformaCreditos.csproj
│   ├── Program.cs
│   ├── Controllers/
│   ├── Data/
│   ├── Hubs/
│   ├── Messaging/
│   ├── Models/
│   ├── Views/
│   └── wwwroot/
└── docs/
```

## Ejecución local

### 1. Restaurar y compilar

Desde la raíz del repositorio:

```bash
dotnet restore PlataformaCreditos/PlataformaCreditos.csproj
dotnet build PlataformaCreditos/PlataformaCreditos.csproj
```

### 2. Levantar Redis local con Docker

```bash
docker run --name plataforma-redis \
  -p 6379:6379 \
  -d redis:7-alpine
```

Para detenerlo o eliminarlo posteriormente:

```bash
docker stop plataforma-redis
docker rm plataforma-redis
```

La aplicación usa `localhost:6379` por defecto mediante `ConnectionStrings:Redis`. También puede configurarse con `Redis:ConnectionString` o con la variable de entorno `ConnectionStrings__Redis`.

### 3. Configurar RabbitMQ/CloudAMQP

La aplicación utiliza AMQPS. Define la URI en la sesión de terminal:

```powershell
$env:RABBITMQ_URI = "amqps://USUARIO:PASSWORD@HOST/VHOST"
```

También se admite la construcción mediante `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD` y `RABBITMQ_VHOST`. La cola utilizada es `solicitudes.notificaciones`.

En Linux/macOS:

```bash
export RABBITMQ_URI='amqps://USUARIO:PASSWORD@HOST/VHOST'
```

No se recomienda usar el usuario `guest` de RabbitMQ para un despliegue real.

### 4. Variables de configuración

| Variable | Uso | Valor local recomendado |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Conexión de SQLite | `Data Source=app.db` |
| `ConnectionStrings__Redis` | Redis para caché y sesión | `localhost:6379` |
| `Redis__ConnectionString` | Alternativa para Redis | `localhost:6379` |
| `RABBITMQ_URI` | URI AMQPS de RabbitMQ/CloudAMQP | `amqps://...` |
| `RABBITMQ_HOST` | Host de RabbitMQ | Host de CloudAMQP |
| `RABBITMQ_PORT` | Puerto AMQPS | `5671` |
| `RABBITMQ_USER` | Usuario de RabbitMQ | Usuario del broker |
| `RABBITMQ_PASSWORD` | Contraseña de RabbitMQ | Contraseña del broker |
| `RABBITMQ_VHOST` | Virtual host de RabbitMQ | Virtual host del broker |
| `ASPNETCORE_ENVIRONMENT` | Entorno de la aplicación | `Development` |
| `ASPNETCORE_URLS` | URL HTTP del contenedor | `http://+:8080` |

Los secretos no deben guardarse en `appsettings.json` ni subirse al repositorio. Usa variables de entorno, User Secrets o el panel de secretos de Render.

### 5. Ejecutar la aplicación

```bash
dotnet run --project PlataformaCreditos/PlataformaCreditos.csproj
```

La aplicación aplica las migraciones y ejecuta `DbInitializer.SeedAsync(...)` automáticamente al iniciar. También aplica la autenticación de Identity, el middleware de sesión y las migraciones pendientes.

El sitio queda disponible normalmente en:

```text
https://localhost:5001
```

Los usuarios iniciales que crea el inicializador son:

- Analista: `analista@plataforma.com` / `Analista123!`.
- Cliente: `cliente@plataforma.com` / `Cliente123!`.

Cambia estas credenciales antes de utilizar el sistema en un entorno compartido.

## Migraciones de Entity Framework

El proyecto incluye las migraciones en `PlataformaCreditos/Data/Migrations`.

### Crear una migración

```bash
dotnet ef migrations add NombreDeLaMigracion \
  --project PlataformaCreditos/PlataformaCreditos.csproj \
  --output-dir Data/Migrations
```

### Aplicar migraciones

```bash
dotnet ef database update \
  --project PlataformaCreditos/PlataformaCreditos.csproj
```

### Listar migraciones

```bash
dotnet ef migrations list \
  --project PlataformaCreditos/PlataformaCreditos.csproj
```

`DbInitializer` ejecuta `Database.MigrateAsync()` durante el arranque. El comando `database update` es útil para despliegues automatizados o entornos donde se quiera aplicar una migración antes de iniciar el proceso.

## Ejecución con Docker

Construir la imagen desde la raíz:

```bash
docker build -t plataforma-creditos:latest .
```

Ejecutar usando un archivo local de variables:

```bash
docker run --rm -p 8080:8080 \
  --env-file .env \
  plataforma-creditos:latest
```

Para desarrollo con SQLite persistente, monta un volumen y define la ruta de la base de datos:

```bash
docker run --rm -p 8080:8080 \
  --env-file .env \
  -e ConnectionStrings__DefaultConnection="Data Source=/data/app.db" \
  -v plataforma-data:/data \
  plataforma-creditos:latest
```

El `Dockerfile` utiliza imágenes oficiales de .NET 10, escucha en el puerto `8080` y es compatible con el detector de Docker de Render.

## Despliegue en Render

### 1. Preparar el repositorio

```bash
git add Dockerfile .dockerignore README.md
git commit -m "chore: preparar despliegue Docker"
git push origin main
```

### 2. Crear el Web Service

1. Entra en Render y selecciona **New > Web Service**.
2. Conecta el repositorio de GitHub.
3. Selecciona la rama que contiene el `Dockerfile`.
4. Render debe detectar automáticamente:
   - **Runtime**: Docker.
   - **Dockerfile path**: `Dockerfile`.
   - **Docker context**: `.` (raíz del repositorio).
5. Usa `/` como health check path.

### 3. Configurar el disco persistente de SQLite

SQLite necesita un disco persistente en Render; el sistema de archivos del contenedor es efímero.

1. En el Web Service, abre **Disks**.
2. Añade un disco montado en `/var/data`.
3. Define la variable:

```text
ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db
```

4. Guarda la configuración y despliega nuevamente. `DbInitializer` aplicará las migraciones sobre el archivo del disco.

### 4. Configurar Redis

Crea o vincula un Redis de Render y define una de estas variables:

```text
ConnectionStrings__Redis=redis://<host>:<port>
```

o:

```text
Redis__ConnectionString=redis://<host>:<port>
```

No incluyas contraseñas de Redis en el repositorio. Si el proveedor entrega una variable `REDIS_URL`, copia su valor a una de las variables anteriores en el panel de Render.

### 5. Configurar RabbitMQ/CloudAMQP

Define la conexión AMQPS en el panel de Render:

```text
RABBITMQ_URI=amqps://<usuario>:<password>@<host>/<vhost>
```

Si el proveedor usa nombres de host separados, también puedes definir:

```text
RABBITMQ_HOST=<host>
RABBITMQ_PORT=5671
RABBITMQ_USER=<usuario>
RABBITMQ_PASSWORD=<password>
RABBITMQ_VHOST=<vhost>
```

La cola es `solicitudes.notificaciones`. El consumidor usa ACK manual y la entidad `Notificacion.MessageId` posee una restricción única para evitar notificaciones duplicadas.

### 6. Variables finales recomendadas para Render

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db
ConnectionStrings__Redis=<url-de-redis>
RABBITMQ_URI=<uri-amqps-de-cloudamqp>
```

Render debe escuchar en el puerto `8080`; no cambies el `EXPOSE` del `Dockerfile` sin actualizar el comando de arranque.

### 7. Verificar el despliegue

1. Abre `/` y confirma que la aplicación responde.
2. Inicia sesión con un usuario inicial o registra uno nuevo.
3. Abre `Solicitudes/MisSolicitudes` y comprueba la conexión de SignalR.
4. Revisa los logs de Render para confirmar:
   - La aplicación inició en el puerto `8080`.
   - La migración de SQLite terminó correctamente.
   - El consumidor RabbitMQ se conectó o está reintentando de forma controlada.
5. Si la aplicación se reinicia, confirma que las notificaciones y solicitudes siguen en `/var/data/app.db`.

## Solución de problemas

- **Redis no disponible**: verifica el host, puerto, contraseña y que la URL use el formato correcto. En Render, comprueba que el servicio Redis esté vinculado al Web Service.
- **RabbitMQ no conecta**: confirma que la URI usa `amqps`, que el vhost está codificado correctamente y que las credenciales son válidas.
- **Las solicitudes desaparecen al reiniciar**: monta un disco persistente en `/var/data` y usa `Data Source=/var/data/app.db`.
- **SignalR no conecta**: usa el dominio HTTPS de Render, no una URL HTTP local, y verifica que el proxy permita WebSockets.
- **La base de datos no tiene tablas**: ejecuta `dotnet ef database update` o revisa los permisos de escritura del disco.
