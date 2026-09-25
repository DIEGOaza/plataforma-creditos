using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace PlataformaCreditos.Infrastructure;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
{
    private readonly IConfiguration _configuration;

    public RabbitMqConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration["RabbitMq:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = _configuration.GetConnectionString("RabbitMq");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ no está configurado. Define RabbitMq:ConnectionString o ConnectionStrings:RabbitMq.");
        }

        Uri connectionUri;
        try
        {
            connectionUri = new Uri(connectionString, UriKind.Absolute);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidOperationException(
                "RabbitMq:ConnectionString no contiene una URI válida.",
                exception);
        }

        if (!string.Equals(
                connectionUri.Scheme,
                "amqps",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RabbitMq:ConnectionString debe utilizar el esquema AMQPS.");
        }

        if (string.Equals(connectionUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            connectionUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "RabbitMq:ConnectionString no puede apuntar a localhost ni a una dirección loopback.");
        }

        var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
        factory.AutomaticRecoveryEnabled = true;
        factory.TopologyRecoveryEnabled = true;

        return factory.CreateConnectionAsync("plataforma-creditos", cancellationToken);
    }
}
