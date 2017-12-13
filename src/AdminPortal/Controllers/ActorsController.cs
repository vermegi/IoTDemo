using IoTDemo.IoTDeviceActor.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Query;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdminPortal.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ActorsController : Controller
    {
        public ActorsController() { }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var activeActors = new List<ActorData>();
            int partitioncount = 0;

            ServicePartitionList partitions;
            var cancellationToken = new CancellationToken();

            using (var client = new FabricClient())
            {
                partitions = await client.QueryManager.GetPartitionListAsync(new Uri("fabric:/IotDemoApp/IoTDeviceActorService"));
                partitioncount = partitions.Count;
            }

            for (var i = 0; i < partitioncount; i++) 
            {
                var actorServiceProxy = ActorServiceProxy.Create(new Uri("fabric:/IotDemoApp/IoTDeviceActorService"), i);

                ContinuationToken continuationToken = null;

                do
                {
                    var page = await actorServiceProxy.GetActorsAsync(continuationToken, cancellationToken);
                    activeActors.AddRange(page.Items.Select(a => new ActorData { Actor = a, Partition = i}));
                    continuationToken = page.ContinuationToken;
                }
                while (continuationToken != null);
            }

            //foreach(var actor in activeActors)
            //{
            //    var theActor = ActorProxy.Create<IIoTDeviceActor>(actor.Actor.ActorId, new Uri("fabric:/IotDemoApp/IoTDeviceActorService"));
            //    actor.LastMessage = await theActor.GetLastDeviceMessage(cancellationToken);
            //    actor.NumberOfMessages = await theActor.GetNumberOfMessages(cancellationToken);
            //}

            return Json(new { PartitionCount = partitioncount, ActorCount = activeActors.Count(), Actors = activeActors.OrderBy(a => a.Actor.ActorId)});
        }

    }

    public class ActorData
    {
        public ActorInformation Actor { get; set; }
        public int Partition { get; set; }

        public string LastMessage { get; set; }

        public int NumberOfMessages { get; set; }
    }
}
