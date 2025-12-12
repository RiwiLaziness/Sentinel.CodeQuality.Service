using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Sentinel.CodeQuality.Service.DTOs;

namespace Sentinel.CodeQuality.Service.Publishers;
public class ReportPublisher : IReportPublisher, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _syncRoot = new();
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConnectionFactory _factory;
    private bool _disposed = false;

    public ReportPublisher(IConfiguration configuration, ILogger<ReportPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _exchangeName = configuration["RabbitMQ:Exchange"] ?? "scan.results";
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "scan.result.final";

        _factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672,
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = false,
            AutomaticRecoveryEnabled = false
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Intenta crear conexión al iniciar (no bloquear demasiado)
        TryEnsureConnectionAsync().GetAwaiter().GetResult();
    }

    private async Task TryEnsureConnectionAsync()
    {
        if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen) return;

        // Lock para evitar reconexiones concurrentes
        lock (_syncRoot)
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen) return;

            var attempt = 0;
            var maxAttempts = 5;
            Exception? lastEx = null;

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    _logger.LogInformation("RabbitMQ: intentando conectar (intento {Attempt}/{Max})", attempt, maxAttempts);

                    // Si había conexión previa, limpiarla
                    try
                    {
                        _channel?.Close();
                    }
                    catch { /* ignore */ }
                    try
                    {
                        _connection?.Close();
                    }
                    catch { /* ignore */ }

                    _connection = _factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    // Publisher confirms
                    _channel.ConfirmSelect();

                    // Declarar exchange durable
                    _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);

                    // Registrar handlers para detectar cierre
                    _connection.ConnectionShutdown += (s, e) =>
                    {
                        _logger.LogWarning("RabbitMQ: conexión cerrada: {Reason}", e.ReplyText);
                    };

                    _channel.ModelShutdown += (s, e) =>
                    {
                        _logger.LogWarning("RabbitMQ: canal cerrado: {ReplyText}", e.ReplyText);
                    };

                    _logger.LogInformation("RabbitMQ: conexión establecida con exchange '{Exchange}'", _exchangeName);
                    lastEx = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    _logger.LogWarning(ex, "RabbitMQ: fallo al conectar en intento {Attempt}", attempt);
                    // Backoff exponencial
                    var delay = Math.Min(2000 * attempt, 10000);
                    Thread.Sleep(delay);
                }
            }

            if (lastEx != null)
            {
                _logger.LogError(lastEx, "RabbitMQ: no fue posible crear la conexión después de {MaxAttempts} intentos", maxAttempts);
                // No throw aquí para no romper constructor; publish intentará reconectar cuando sea necesario.
            }
        }
    }

    public async Task PublishFinalResultAsync(ScanFinalResultDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        var payload = JsonSerializer.Serialize(dto, _jsonOptions);
        var body = Encoding.UTF8.GetBytes(payload);

        var props = _channel?.CreateBasicProperties();
        if (props != null)
        {
            props.DeliveryMode = 2; // persistent
            props.ContentType = "application/json";
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        const int maxPublishAttempts = 3;
        var attempt = 0;
        Exception? lastEx = null;

        while (attempt < maxPublishAttempts)
        {
            attempt++;
            try
            {
                // Asegurarnos conexión/canal estén disponibles
                await TryEnsureConnectionAsync();

                if (_channel == null || _channel.IsClosed)
                    throw new InvalidOperationException("RabbitMQ channel is not available.");

                // Publicar
                _channel.BasicPublish(exchange: _exchangeName, routingKey: _routingKey, basicProperties: props, body: body);

                // Esperar confirm
                var confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));
                if (!confirmed)
                {
                    throw new Exception("Publisher confirm failed (timeout or nack).");
                }

                _logger.LogInformation("Publicado resultado del scan {ScanId} en exchange '{Exchange}'", dto.ScanId, _exchangeName);
                return;
            }
            catch (AlreadyClosedException ace)
            {
                lastEx = ace;
                _logger.LogWarning(ace, "RabbitMQ: canal/conexión cerrada al publicar (intento {Attempt})", attempt);
                // Forzar recreación de conexión en el siguiente intento
                InvalidateConnection();
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "RabbitMQ: error publicando (intento {Attempt})", attempt);
                // Intentar recrear conexión y reintentar
                InvalidateConnection();
            }

            // Backoff antes del siguiente intento
            var backoff = 200 * (int)Math.Pow(2, attempt); // 400ms, 800ms, ...
            await Task.Delay(backoff);
        }

        _logger.LogError(lastEx, "RabbitMQ: no fue posible publicar el resultado tras {Attempts} intentos", maxPublishAttempts);
        throw new InvalidOperationException("Failed to publish final scan result to RabbitMQ after retries.", lastEx);
    }

    private void InvalidateConnection()
    {
        lock (_syncRoot)
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
            _channel = null;
            _connection = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_syncRoot)
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
            _channel?.Dispose();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
