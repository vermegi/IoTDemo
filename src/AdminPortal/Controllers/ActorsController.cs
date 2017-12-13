using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Query;
using System;
using System.Collections.Generic;
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
            var activeActors = new List<ActorInformation>();

            for (var i = 0; i < 2; i++) //TODO: change upper limit based on query to service fabric manager
            {
                var actorServiceProxy = ActorServiceProxy.Create(new Uri("fabric:/IotDemoApp/IoTDeviceActorService"), i);

                ContinuationToken continuationToken = null;
                var cancellationToken = new CancellationToken();

                do
                {
                    var page = await actorServiceProxy.GetActorsAsync(continuationToken, cancellationToken);
                    activeActors.AddRange(page.Items);
                    continuationToken = page.ContinuationToken;
                }
                while (continuationToken != null);
            }

            return Json(activeActors.Count);
        }

    }
}
