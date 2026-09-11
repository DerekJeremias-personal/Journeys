using Journeys.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Queues
{
    public interface IQueueAdapter
    {
        Task<T> ReceiveAsync<T>(string queueName, CancellationToken cancellationToken);

        Task<IEnumerable<T>> ReceiveBulkAsync<T>(string queueName, int maxMessages, CancellationToken cancellationToken);

        Task<bool> SendAsync<T>(T payload, string queueName, CancellationToken cancellationToken);

        Task<bool> SendBulkAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken);
    }
}
