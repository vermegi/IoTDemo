using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace IoTDemo.IoTDeviceActor.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface IIoTDeviceActor : IActor
    {
        Task SendDeviceMessage(string message, CancellationToken cancellationToken);
        Task<string> GetLastDeviceMessage(CancellationToken cancellationToken);

        Task<int> GetNumberOfMessages(CancellationToken cancellation);
    }
}
