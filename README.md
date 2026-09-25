# PlataformaCreditos

Plataforma de solicitudes de crédito desarrollada con ASP.NET Core, SignalR, Redis y RabbitMQ/CloudAMQP.

## Evidencias de Pruebas

### Pregunta 6: Notificaciones WebSocket (SignalR)

#### Flujo de notificación en tiempo real

1. `SolicitudesHub` se expone en `/hubs/solicitudes` y está protegido con `[Authorize]`. Una conexión anónima no puede completar el handshake y recibe `401 Unauthorized`.
2. El usuario propietario se conecta al Hub mediante la cookie de autenticación. Al conectarse, el servidor lo agrega al grupo `usuario:{UsuarioId}`.
3. Cuando un analista aprueba o rechaza una solicitud, `AnalistaController` actualiza SQLite, invalida la caché de Redis y emite el evento `SolicitudEstadoActualizado`.
4. El evento se envía únicamente al grupo del propietario mediante `Clients.Group(...)`; no se utiliza una difusión global.
5. `wwwroot/js/solicitudes.js` escucha el evento y actualiza el estado, el motivo de rechazo y los mensajes de la vista `MisSolicitudes` o `Detalle` sin recargar la página.
6. El cliente muestra los estados de conexión, reconexión y reconexión manual después de cerrar la conexión.

#### Evidencias

![Conexión WebSocket / WS](docs/p6-websocket-ws.png)

![Resultado en tiempo real](docs/p6-notificacion-cliente.png)

![Conexión anónima rechazada](docs/p6-conexion-anonima-401.png)

### Pregunta 7: Mensajería Asíncrona (Cloud MQ / CloudAMQP)

#### Requisitos

- Configurar `RabbitMq:ConnectionString` con la URL AMQPS de CloudAMQP, mediante `appsettings.Development.json`, una variable de entorno o un secreto.
- La cola utilizada es `solicitudes.notificaciones`.
- La aplicación ejecuta las migraciones de SQLite antes de iniciar el consumidor.
- El consumidor utiliza `autoAck: false` y confirma cada mensaje procesado con `BasicAckAsync`.
- `Notificacion.MessageId` tiene una restricción de unicidad en SQLite para hacer idempotente el procesamiento.

#### Probar con el consumidor deshabilitado

En PowerShell, desde la raíz del repositorio:

```powershell
$env:RabbitMq__ConsumerEnabled = "false"
dotnet run --project .\PlataformaCreditos
```

1. Confirma que el proceso inicia sin registrar el `BackgroundService` consumidor.
2. Publica una solicitud nueva para que el publisher envíe el mensaje `SolicitudRegistrada` a `solicitudes.notificaciones`.
3. Revisa CloudAMQP y verifica que el mensaje permanece pendiente en la cola porque no existe un consumer activo.
4. Captura la evidencia de la cola con el mensaje pendiente.

![Mensaje pendiente en CloudAMQP](docs/p7-mensaje-pendiente.png)

#### Probar con el consumidor habilitado

Detén el proceso anterior y vuelve a iniciarlo con:

```powershell
$env:RabbitMq__ConsumerEnabled = "true"
dotnet run --project .\PlataformaCreditos
```

1. Publica otra solicitud o reencola el mensaje pendiente.
2. El `SolicitudesNotificationConsumer` consume el mensaje, valida el `MessageId`, guarda una sola entidad `Notificacion` en SQLite y envía el ACK manual.
3. Verifica en CloudAMQP que la cola queda vacía después del procesamiento.
4. Consulta `Solicitudes/MisNotificaciones` como el usuario propietario y confirma que la notificación aparece.
5. Captura la evidencia de la cola vacía y de la notificación única.

![Cola vacía y notificación única](docs/p7-cola-vacia-notificacion.png)

#### Probar la idempotencia

1. Con el consumidor habilitado, procesa un mensaje `SolicitudRegistrada` válido.
2. Reencola o vuelve a publicar exactamente el mismo contenido, conservando el mismo `MessageId`.
3. El consumidor consulta `Notificaciones` por `MessageId` antes de insertar y la restricción única de SQLite evita una segunda fila.
4. El mensaje duplicado se confirma con ACK sin crear otra notificación.

![Idempotencia sin duplicados](docs/p7-idempotencia.png)
