using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using IoTDemo.IoTDeviceActor.Interfaces;

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
            return this.StateManager.GetStateAsync<string>("lastState", cancellationToken);
        }

        public Task SendDeviceMessage(string message, CancellationToken cancellationToken)
        {
            return StateManager.AddOrUpdateStateAsync("lastState", message, (key, value) => message, cancellationToken);
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization

            return this.StateManager.TryAddStateAsync("lastState", string.Empty);
        }
    }
}
