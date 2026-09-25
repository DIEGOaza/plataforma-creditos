using Microsoft.Extensions.Configuration;

namespace PlataformaCreditos.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string DefaultQueueName = "solicitudes.notificaciones";

    private RabbitMqOptions(string queueName, bool consumerEnabled)
    {
        QueueName = queueName;
        ConsumerEnabled = consumerEnabled;
    }

    public string QueueName { get; }

    public bool ConsumerEnabled { get; }

    public static RabbitMqOptions FromConfiguration(IConfiguration configuration)
    {
        var queueName = configuration["RabbitMq:QueueName"];
        var consumerEnabled = configuration.GetValue("RabbitMq:ConsumerEnabled", true);
        return new RabbitMqOptions(
            string.IsNullOrWhiteSpace(queueName) ? DefaultQueueName : queueName,
            consumerEnabled);
    }
}
