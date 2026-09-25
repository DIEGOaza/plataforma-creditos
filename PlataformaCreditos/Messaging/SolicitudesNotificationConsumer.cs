using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Messaging;

public sealed class SolicitudesNotificationConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReconexionDelay = TimeSpan.FromSeconds(10);

    private readonly IRabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SolicitudesNotificationConsumer> _logger;

    public SolicitudesNotificationConsumer(
        IRabbitMqConnectionFactory connectionFactory,
        RabbitMqOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<SolicitudesNotificationConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    await EsperarReintentoAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "No fue posible consumir la cola de RabbitMQ. Se intentará nuevamente.");
                await EsperarReintentoAsync(stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        await using var connection = await _connectionFactory
            .CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) =>
        {
            shutdown.TrySetResult();
            return Task.CompletedTask;
        };
        channel.ChannelShutdownAsync += (_, _) =>
        {
            shutdown.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await ProcesarMensajeAsync(channel, eventArgs, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        using var cancellationRegistration = stoppingToken.Register(
            () => shutdown.TrySetCanceled(stoppingToken));
        await shutdown.Task;
    }

    private async Task ProcesarMensajeAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        try
        {
            var messageType = eventArgs.BasicProperties.Type;
            if (!string.IsNullOrWhiteSpace(messageType) &&
                !string.Equals(messageType, nameof(SolicitudRegistrada), StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Se descartó un mensaje con tipo no soportado: {Tipo}.",
                    messageType);
                await ConfirmarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
                return;
            }

            var message = JsonSerializer.Deserialize<SolicitudRegistrada>(
                eventArgs.Body.Span,
                JsonOptions);

            if (message is null ||
                message.MessageId == Guid.Empty ||
                message.SolicitudId <= 0 ||
                message.ClienteId <= 0 ||
                string.IsNullOrWhiteSpace(message.UsuarioId))
            {
                _logger.LogWarning("Se descartó un mensaje SolicitudRegistrada inválido.");
                await ConfirmarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
                return;
            }

            var messageId = message.MessageId.ToString("D");
            var amqpMessageId = eventArgs.BasicProperties.MessageId;
            if (!string.IsNullOrWhiteSpace(amqpMessageId) &&
                !string.Equals(amqpMessageId, messageId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Se descartó un mensaje con MessageId inconsistente.");
                await ConfirmarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var yaExiste = await context.Notificaciones.AnyAsync(notificacion =>
                notificacion.MessageId == messageId);

            if (!yaExiste)
            {
                context.Notificaciones.Add(new Notificacion
                {
                    MessageId = messageId,
                    SolicitudId = message.SolicitudId,
                    UsuarioId = message.UsuarioId,
                    Tipo = nameof(SolicitudRegistrada),
                    Mensaje = $"La solicitud #{message.SolicitudId} fue registrada correctamente.",
                    FechaCreacion = DateTime.UtcNow
                });

                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Una entrega duplicada puede coincidir con otra instancia
                    // del consumidor; la restricción única decide el resultado.
                    context.ChangeTracker.Clear();
                    var duplicadoPorCarrera = await context.Notificaciones.AnyAsync(
                        notificacion => notificacion.MessageId == messageId);
                    if (!duplicadoPorCarrera)
                    {
                        throw;
                    }
                }
            }

            await ConfirmarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Se descartó un mensaje con JSON inválido.");
            await ConfirmarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // La CancellationToken indique que el consumidor se está detiendo.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "No fue posible procesar el mensaje; se reencolará.");
            await ReencolarAsync(channel, eventArgs.DeliveryTag, stoppingToken);
        }
    }

    private async Task ConfirmarAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.BasicAckAsync(
                deliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible enviar el ACK manual.");
        }
    }

    private async Task ReencolarAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible reencolar el mensaje.");
        }
    }

    private async Task EsperarReintentoAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(ReconexionDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
