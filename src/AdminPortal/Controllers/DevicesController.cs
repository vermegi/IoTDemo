using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var result = new { CreatingDevices = false, SendingData = true };

            return Json(result);
        }

        [HttpPut("togglecreate")]
        public async Task<IActionResult> ToggleCreateDevices()
        {
            return Ok();
        }

        [HttpPut("togglesend")]
        public async Task<IActionResult> ToggleSendDataFromDevices()
        {
            return Ok();
        }
    }
}
