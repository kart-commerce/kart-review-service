namespace Kart.Review.Infrastructure.Messaging;

/// <summary>Binds the `"RabbitMq"` config section. Everything topology-shaped (exchanges/queues/routing keys/retry ladders/DLQs) lives in `contracts/message-bus-manifest.json`, never here.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
