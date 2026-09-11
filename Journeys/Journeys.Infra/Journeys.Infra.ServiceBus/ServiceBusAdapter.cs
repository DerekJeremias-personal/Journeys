using Azure.Messaging.ServiceBus;
using Journeys.Core.Interfaces.Queues;
using Journeys.Core.Utility;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Journeys.Infra.ServiceBus
{
    public class ServiceBusAdapter : IQueueAdapter, IAsyncDisposable
    {
        private readonly string _connectionString;
        private ServiceBusClient client;
        private Dictionary<string, ServiceBusSender> senders = new Dictionary<string, ServiceBusSender>();
        private readonly ILogger<ServiceBusAdapter> _logger;
        private ServiceBusClientOptions options = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpWebSockets
        };

        public ServiceBusAdapter(ILogger<ServiceBusAdapter> logger, string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            _logger = logger;
            _connectionString = connectionString;
            client = new ServiceBusClient(_connectionString, options);
        }
        public Task<T> ReceiveAsync<T>(string queueName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> ReceiveBulkAsync<T>(string queueName, int maxMessages, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private ServiceBusSender GetSender(string queueName)
        {
            if (!senders.ContainsKey(queueName))
            {
                lock (senders)
                {
                    if (!senders.ContainsKey(queueName))
                    {
                        senders.Add(queueName, client.CreateSender(queueName));
                    }
                }
            }

            var sender = senders[queueName];
            return sender;
        }

        private ServiceBusMessage CreateMessage<T>(T payload)
        {
            var json = JsonUtility.Serialize(payload, _logger);
            var message = new ServiceBusMessage(json);
            return message;
        }

        public async Task<bool> SendAsync<T>(T payload, string queueName, CancellationToken cancellationToken)
        {
            var sender = GetSender(queueName);
            var msg = CreateMessage(payload);

            try
            {
                await sender.SendMessageAsync(msg, cancellationToken);
                return true;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex);
                return false;
            }

        }

        public async Task<bool> SendBulkAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken)
        {
            var sender = GetSender(queueName);
            using (var batch = await sender.CreateMessageBatchAsync())
            {
                foreach(var m in messages)
                {
                    var msg = CreateMessage(m);
                    if (!batch.TryAddMessage(msg))
                    {
                        Log.Debug(($"ServiceBusAdapter::SendBulkAsync - Message too large to add to batch"));
                        throw new Exception("ServiceBusAdapter::SendBulkAsync - Message too large to add to batch.");
                    }
                }

                await sender.SendMessagesAsync(batch, cancellationToken);
            }

            return true;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var s in senders.Values)
            {
                try
                {
                    await s.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }
            }

            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }
    }
}
