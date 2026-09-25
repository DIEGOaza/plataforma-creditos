using System.Text.Json;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Models;
using RabbitMQ.Client;

namespace PlataformaCreditos.Messaging;

public interface ISolicitudNotificationPublisher
{
    Task PublishAsync(
        SolicitudCredito solicitud,
        string usuarioId,
        CancellationToken cancellationToken = default);
}

public sealed class SolicitudNotificationPublisher : ISolicitudNotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;

    public SolicitudNotificationPublisher(
        IRabbitMqConnectionFactory connectionFactory,
        RabbitMqOptions options)
    {
        _connectionFactory = connectionFactory;
        _options = options;
    }

    public async Task PublishAsync(
        SolicitudCredito solicitud,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new InvalidOperationException(
                "La solicitud debe pertenecer a un usuario autenticado.");
        }

        var messageId = Guid.NewGuid();
        var message = new SolicitudRegistrada(
            messageId,
            solicitud.Id,
            solicitud.ClienteId,
            usuarioId,
            solicitud.MontoSolicitado,
            solicitud.FechaSolicitud);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await using var connection = await _connectionFactory
            .CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = messageId.ToString("D"),
            Persistent = true,
            Type = nameof(SolicitudRegistrada)
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            mandatory: true,
            basicProperties: properties,
            body,
            cancellationToken: cancellationToken);
    }
}
