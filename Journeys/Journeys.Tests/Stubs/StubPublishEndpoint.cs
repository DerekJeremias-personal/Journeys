using MassTransit;
using System;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    public class StubPublishEndpoint : IPublishEndpoint
    {
        public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        {
            return new StubConnectHandle();
        }

        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish<T>(T message, IPipe<PublishContext<T>> pipe, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish<T>(T message, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish(object message, CancellationToken cancellationToken = default)
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish(object message, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, IPipe<PublishContext<T>> pipe, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        public Task Publish<T>(object values, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default) where T : class
        {
            // Stub implementation - do nothing
            return Task.CompletedTask;
        }

        private class StubConnectHandle : ConnectHandle
        {
            public void Disconnect()
            {
                // Stub implementation - do nothing
            }

            public void Dispose()
            {
                // Stub implementation - do nothing
            }
        }
    }
} 