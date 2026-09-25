using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace PlataformaCreditos.Infrastructure;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection?> CreateConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqRetryableException : Exception
{
    public RabbitMqRetryableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
{
    private const string ConfiguracionCloudAmqpMessage =
        "RabbitMQ/CloudAMQP no está configurado. Configura la URL de CloudAMQP en appsettings.Development.json.";

    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConnectionFactory> _logger;

    public RabbitMqConnectionFactory(
        IConfiguration configuration,
        ILogger<RabbitMqConnectionFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IConnection?> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration["RabbitMq:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = _configuration.GetConnectionString("RabbitMq");
        }

        if (string.IsNullOrWhiteSpace(connectionString) ||
            ContieneValorMarcador(connectionString))
        {
            _logger.LogWarning(ConfiguracionCloudAmqpMessage);
            return null;
        }

        Uri connectionUri;
        try
        {
            connectionUri = new Uri(connectionString, UriKind.Absolute);
        }
        catch (UriFormatException exception)
        {
            _logger.LogWarning(
                exception,
                "RabbitMq:ConnectionString no contiene una URI válida. Configura la URL de CloudAMQP en appsettings.Development.json.");
            return null;
        }

        if (!string.Equals(
                connectionUri.Scheme,
                "amqps",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(connectionUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            connectionUri.IsLoopback)
        {
            _logger.LogWarning(ConfiguracionCloudAmqpMessage);
            return null;
        }

        try
        {
            var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
            factory.AutomaticRecoveryEnabled = true;
            factory.TopologyRecoveryEnabled = true;

            return await factory.CreateConnectionAsync(
                "plataforma-creditos",
                cancellationToken);
        }
        catch (BrokerUnreachableException exception)
        {
            _logger.LogError(
                exception,
                "No fue posible conectar con RabbitMQ/CloudAMQP. Se reintentará la conexión.");
            throw new RabbitMqRetryableException(
                "No fue posible conectar con RabbitMQ/CloudAMQP.",
                exception);
        }
    }

    private static bool ContieneValorMarcador(string connectionString)
    {
        return connectionString.Contains("amqp.cloudamqp.com", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("TU_USUARIO", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("TU_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("TU_VHOST", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
