using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace TasksService.Services;

public class RabbitMQPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private const string ExchangeName = "taskflow.events";

    public RabbitMQPublisher(IConfiguration config)
    {
        var rabbitUrl = config["RABBITMQ_URL"] ?? "amqp://admin:admin@rabbitmq:5672";

        var factory = new ConnectionFactory { Uri = new Uri(rabbitUrl) };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        // Declaramos el exchange — si no existe, lo crea
        _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true
        ).GetAwaiter().GetResult();
    }

    public async Task PublishTaskCreated(int taskId, string title, int userId)
    {
        var message = new
        {
            Event = "task.created",
            TaskId = taskId,
            Title = title,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: string.Empty,
            body: body
        );
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _connection?.CloseAsync().GetAwaiter().GetResult();
    }
}