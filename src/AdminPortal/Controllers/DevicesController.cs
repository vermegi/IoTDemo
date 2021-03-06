﻿using IotDemo.DeviceEmulator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System.Net.Http;
using System.Threading.Tasks;


namespace AdminPortal.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class DevicesController : Controller
    {
        private readonly HttpClient _httpClient;

        public DevicesController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var deviceEmulatorClient = ServiceProxy.Create<IDeviceEmulatorService>(new System.Uri("fabric:/IotDemoApp/IotDemo.DeviceEmulator"));

            var status = await deviceEmulatorClient.GetStatus();

            return Json(status);
        }

        [HttpPut("togglecreate")]
        public async Task<IActionResult> ToggleCreateDevices()
        {
            var deviceEmulatorClient = ServiceProxy.Create<IDeviceEmulatorService>(new System.Uri("fabric:/IotDemoApp/IotDemo.DeviceEmulator"));
            await deviceEmulatorClient.ToggleCreateDevices();

            return Ok();
        }

        [HttpPut("togglesend")]
        public async Task<IActionResult> ToggleSendDataFromDevices(int temperature)
        {
            var deviceEmulatorClient = ServiceProxy.Create<IDeviceEmulatorService>(new System.Uri("fabric:/IotDemoApp/IotDemo.DeviceEmulator"));
            await deviceEmulatorClient.ToggleSendData(temperature);

            return Ok();
        }
    }
}
