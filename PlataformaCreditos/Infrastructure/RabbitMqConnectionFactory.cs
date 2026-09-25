using RabbitMQ.Client;

namespace PlataformaCreditos.Infrastructure;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
{
    private readonly RabbitMqOptions _options;

    public RabbitMqConnectionFactory(RabbitMqOptions options)
    {
        _options = options;
    }

    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = _options.ConnectionUri,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        return factory.CreateConnectionAsync("plataforma-creditos", cancellationToken);
    }
}
