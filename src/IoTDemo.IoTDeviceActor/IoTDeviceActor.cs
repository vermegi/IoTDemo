using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using IoTDemo.IoTDeviceActor.Interfaces;
using Microsoft.ServiceBus.Messaging;
using Microsoft.ServiceBus;

namespace IoTDemo.IoTDeviceActor
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class IoTDeviceActor : Actor, IIoTDeviceActor
    {
        private QueueClient _queueClient;

        /// <summary>
        /// Initializes a new instance of IoTDeviceActor
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public IoTDeviceActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public Task<string> GetLastDeviceMessage(CancellationToken cancellationToken)
        {
            return StateManager.GetStateAsync<string>("lastState", cancellationToken);
        }

        public Task<int> GetNumberOfMessages(CancellationToken cancellationToken)
        {
            return StateManager.GetOrAddStateAsync("numberOfMessages", 0, cancellationToken);
        }

        public async Task SendDeviceMessage(string message, CancellationToken cancellationToken)
        {
            await IncrementNumberOfMessages(cancellationToken);

            var lastState = await StateManager.GetStateAsync<string>("lastState", cancellationToken);
            if (message != lastState)
            {
                await SendStateChangeMessage(message, lastState);
            }

            await StateManager.AddOrUpdateStateAsync("lastState", message, (key, value) => message, cancellationToken);
        }

        private async Task SendStateChangeMessage(string message, string lastState)
        {
            var brokeredMessage = new BrokeredMessage(new DeviceStateChangedEvent(this.GetActorId().GetStringId(), lastState, message));
            await Queueclient.SendAsync(brokeredMessage);
        }

        private async Task IncrementNumberOfMessages(CancellationToken cancellationToken)
        {
            var numberOfMessages = await StateManager.GetOrAddStateAsync("numberOfMessages", 0, cancellationToken);
            numberOfMessages++;
            await StateManager.AddOrUpdateStateAsync("numberOfMessages", numberOfMessages, (key, value) => numberOfMessages, cancellationToken);
        }

        private QueueClient Queueclient
        {
            get
            {
                if (_queueClient == null)
                {
                    var sasKeyName = "RootManageSharedAccessKey";
                    var sasKeyValue = "NvY+XbLTscBcCIH/Za9tK1kz76kSlYNlYXnb54glkjM=";
                    var serviceNamespace = "iotdemogittesbns";
                    var queueName = "DeviceChangeEvents";
                    // Create management credentials
                    var credentials = TokenProvider.CreateSharedAccessSignatureTokenProvider(sasKeyName, sasKeyValue);
                    // Create namespace client
                    var namespaceClient = new NamespaceManager(ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty), credentials);
                    var myQueue = namespaceClient.CreateQueue(queueName);
                    var messagingFactory = MessagingFactory.Create(ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty), credentials);
                    var myQueueClient = messagingFactory.CreateQueueClient(queueName);
                }
                return _queueClient;
            }
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");
            StateManager.TryAddStateAsync("numberOfMessages", 0);
            return StateManager.TryAddStateAsync("lastState", string.Empty);
        }
    }
}
