using Microsoft.Extensions.Configuration;

namespace PlataformaCreditos.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string DefaultQueueName = "solicitudes.notificaciones";

    private RabbitMqOptions(string queueName)
    {
        QueueName = queueName;
    }

    public string QueueName { get; }

    public static RabbitMqOptions FromConfiguration(IConfiguration configuration)
    {
        var queueName = configuration["RabbitMq:QueueName"];
        return new RabbitMqOptions(
            string.IsNullOrWhiteSpace(queueName) ? DefaultQueueName : queueName);
    }
}
