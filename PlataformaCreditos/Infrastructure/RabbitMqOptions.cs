namespace PlataformaCreditos.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string DefaultQueueName = "solicitudes.notificaciones";

    private RabbitMqOptions(Uri connectionUri, string queueName)
    {
        ConnectionUri = connectionUri;
        QueueName = queueName;
    }

    public Uri ConnectionUri { get; }

    public string QueueName { get; }

    public static RabbitMqOptions FromEnvironment()
    {
        var configuredUri = Environment.GetEnvironmentVariable("RABBITMQ_URI")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_URL")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_URI");
        var connectionUri = string.IsNullOrWhiteSpace(configuredUri)
            ? ConstruirUriDesdeVariables()
            : new Uri(configuredUri, UriKind.Absolute);

        if (!string.Equals(connectionUri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RabbitMQ debe configurarse mediante una URI AMQPS.");
        }

        return new RabbitMqOptions(connectionUri, DefaultQueueName);
    }

    private static Uri ConstruirUriDesdeVariables()
    {
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME")
            ?? "localhost";
        var portTexto = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        var port = int.TryParse(portTexto, out var puertoConfigurado) ? puertoConfigurado : 5671;
        var usuario = Environment.GetEnvironmentVariable("RABBITMQ_USER")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_USERNAME")
            ?? "guest";
        var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
            ?? "guest";
        var virtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_VIRTUAL_HOST")
            ?? "/";
        var path = virtualHost.StartsWith("/", StringComparison.Ordinal)
            ? virtualHost
            : "/" + virtualHost;

        var builder = new UriBuilder
        {
            Scheme = "amqps",
            Host = host,
            Port = port,
            Path = path,
            UserName = usuario,
            Password = password
        };

        return builder.Uri;
    }
}
