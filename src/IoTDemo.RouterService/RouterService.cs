using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceBus.Messaging;
using Microsoft.ServiceBus;
using Microsoft.ServiceFabric.Data;
using System.Net.Http;
using Microsoft.ServiceFabric.Actors.Client;
using IoTDemo.IoTDeviceActor.Interfaces;
using Microsoft.ServiceFabric.Actors;

namespace IoTDemo.RouterService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class RouterService : StatefulService
    {
        public RouterService(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
        }

        private const string OffsetDictionaryName = "OffsetDictionary";
        private const string EpochDictionaryName = "EpochDictionary";
        private const int OffsetInterval = 5;

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            string iotHubConnectionstring = "HostName=iotdemogittehub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=dUgqwEJE1K2YcQozHO5N+LnamsBH3dzkFzaoL0CGx9g=";

            // These Reliable Dictionaries are used to keep track of our position in IoT Hub.
            // If this service fails over, this will allow it to pick up where it left off in the event stream.
            var offsetDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(OffsetDictionaryName);
            var epochDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>(EpochDictionaryName);

            // Each partition of this service corresponds to a partition in IoT Hub.
            // IoT Hub partitions are numbered 0..n-1, up to n = 32.
            // This service needs to use an identical partitioning scheme. 
            // The low key of every partition corresponds to an IoT Hub partition.
            var partitionInfo = (Int64RangePartitionInformation)Partition.PartitionInfo;
            long servicePartitionKey = partitionInfo.LowKey;

            EventHubReceiver eventHubReceiver = null;
            MessagingFactory messagingFactory = null;

            try
            {
                // Get an EventHubReceiver and the MessagingFactory used to create it.
                // The EventHubReceiver is used to get events from IoT Hub.
                // The MessagingFactory is just saved for later so it can be closed before RunAsync exits.
                var iotHubInfo = await ConnectToIoTHubAsync(iotHubConnectionstring, servicePartitionKey, epochDictionary, offsetDictionary);

                eventHubReceiver = iotHubInfo.Item1;
                messagingFactory = iotHubInfo.Item2;

                if (eventHubReceiver == null || messagingFactory == null)
                    return;

                int offsetIteration = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // It's important to set a low wait time here in lieu of a cancellation token
                        // so that this doesn't block RunAsync from exiting when Service Fabric needs it to complete.
                        // ReceiveAsync is a long-poll operation, so the timeout should not be too low,
                        // yet not too high to block RunAsync from exiting within a few seconds.
                        using (EventData eventData = await eventHubReceiver.ReceiveAsync(TimeSpan.FromSeconds(5)))
                        {
                            if (eventData == null)
                            {
                                continue;
                            }

                            string deviceId = (string)eventData.Properties["DeviceID"];

                            var deviceactorProxy = ActorProxy.Create<IIoTDeviceActor>(new ActorId(deviceId), new Uri("fabric:/IotDemoApp/IoTDeviceActorService"));
                            await deviceactorProxy.SendDeviceMessage((string)eventData.Properties["Temparature"], cancellationToken);

                            ServiceEventSource.Current.ServiceMessage(
                                Context,
                                "Sent event data to actor service '{0}'.",
                                deviceId);


                            if (++offsetIteration % OffsetInterval == 0)
                            {
                                ServiceEventSource.Current.ServiceMessage(
                                        Context,
                                        "Saving offset {0}",
                                        eventData.Offset);

                                using (ITransaction tx = StateManager.CreateTransaction())
                                {
                                    await offsetDictionary.SetAsync(tx, "offset", eventData.Offset);
                                    await tx.CommitAsync();
                                }

                                offsetIteration = 0;
                            }
                        }
                    }
                    catch (TimeoutException te)
                    {
                        // transient error. Retry.
                        ServiceEventSource.Current.ServiceMessage(Context, $"TimeoutException in RunAsync: {te.ToString()}");
                    }
                    catch (FabricTransientException fte)
                    {
                        // transient error. Retry.
                        ServiceEventSource.Current.ServiceMessage(Context, $"FabricTransientException in RunAsync: {fte.ToString()}");
                    }
                    catch (FabricNotPrimaryException)
                    {
                        // not primary any more, time to quit.
                        return;
                    }
                    catch (Exception ex)
                    {
                        ServiceEventSource.Current.ServiceMessage(Context, ex.ToString());

                        throw;
                    }
                }
            }
            finally
            {
                if (messagingFactory != null)
                {
                    await messagingFactory.CloseAsync();
                }
            }
        }

        private async Task<Tuple<EventHubReceiver, MessagingFactory>> ConnectToIoTHubAsync(
            string connectionString,
            long servicePartitionKey,
            IReliableDictionary<string, long> epochDictionary,
            IReliableDictionary<string, string> offsetDictionary)
        {
            try
            {


                // EventHubs doesn't support NetMessaging, so ensure the transport type is AMQP.
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                connectionStringBuilder.TransportType = TransportType.Amqp;

                ServiceEventSource.Current.ServiceMessage(
                    Context,
                    "RouterService connecting to IoT Hub at {0}",
                    String.Join(",", connectionStringBuilder.Endpoints.Select(x => x.ToString())));

                // A new MessagingFactory is created here so that each partition of this service will have its own MessagingFactory.
                // This gives each partition its own dedicated TCP connection to IoT Hub.
                var messagingFactory = MessagingFactory.CreateFromConnectionString(connectionStringBuilder.ToString());
                var eventHubClient = messagingFactory.CreateEventHubClient("messages/events");
                var eventHubRuntimeInfo = await eventHubClient.GetRuntimeInformationAsync();
                EventHubReceiver eventHubReceiver;

                // Get an IoT Hub partition ID that corresponds to this partition's low key.
                // This assumes that this service has a partition count 'n' that is equal to the IoT Hub partition count and a partition range of 0..n-1.
                // For example, given an IoT Hub with 32 partitions, this service should be created with:
                // partition count = 32
                // partition range = 0..31
                string eventHubPartitionId = eventHubRuntimeInfo.PartitionIds[servicePartitionKey];

                using (var tx = StateManager.CreateTransaction())
                {
                    var offsetResult = await offsetDictionary.TryGetValueAsync(tx, "offset", LockMode.Default);
                    var epochResult = await epochDictionary.TryGetValueAsync(tx, "epoch", LockMode.Update);

                    long newEpoch = epochResult.HasValue
                        ? epochResult.Value + 1
                        : 0;

                    if (offsetResult.HasValue)
                    {
                        // continue where the service left off before the last failover or restart.
                        ServiceEventSource.Current.ServiceMessage(
                            Context,
                            "Creating EventHub listener on partition {0} with offset {1}",
                            eventHubPartitionId,
                            offsetResult.Value);

                        eventHubReceiver = await eventHubClient.GetDefaultConsumerGroup().CreateReceiverAsync(eventHubPartitionId, offsetResult.Value, newEpoch);
                    }
                    else
                    {
                        // first time this service is running so there is no offset value yet.
                        // start with the current time.
                        ServiceEventSource.Current.ServiceMessage(
                            Context,
                            "Creating EventHub listener on partition {0} with offset {1}",
                            eventHubPartitionId,
                            DateTime.UtcNow);

                        eventHubReceiver =
                            await
                                eventHubClient.GetDefaultConsumerGroup()
                                    .CreateReceiverAsync(eventHubPartitionId, DateTime.UtcNow, newEpoch);
                    }

                    // epoch is recorded each time the service fails over or restarts.
                    await epochDictionary.SetAsync(tx, "epoch", newEpoch);
                    await tx.CommitAsync();
                }

                return new Tuple<EventHubReceiver, MessagingFactory>(eventHubReceiver, messagingFactory);
            }
            catch(Exception exc)
            {
                ServiceEventSource.Current.ServiceMessage(Context, "couldn't create eventhub listener: {0}", exc.Message);
            }
            return new Tuple<EventHubReceiver, MessagingFactory>(null, null);
        }

    }
}
