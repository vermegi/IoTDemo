using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace IotDemo.DeviceEmulator
{
    public interface IDeviceEmulatorService : IService
    {
        Task ToggleCreateDevices();
        Task  ToggleSendData();

        Task<DeviceEmulatorData> GetStatus();
    }

    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class DeviceEmulator : StatelessService, IDeviceEmulatorService
    {
        private string _connectionstring = "HostName=iotdemogittehub.azure-devices.net;SharedAccessKeyName=registryReadWrite;SharedAccessKey=uajb2qStqre9hgwPJ1dBY91rNn1h5Wo+dl305nHkVSs=";
        private RegistryManager _registryManager;
        private bool _creatingDevices = false;
        private bool _sendingData = false;

        public DeviceEmulator(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { new ServiceInstanceListener(context => this.CreateServiceRemotingListener(context) ) };
        }

        private RegistryManager MyRegistryManager
        {
            get
            {
                if (_registryManager == null)
                {
                    _registryManager = RegistryManager.CreateFromConnectionString(_connectionstring);
                }
                return _registryManager;
            }
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_creatingDevices)
                    await RegisterSomeDevices();
                if (_sendingData)
                    await SendDataFromSomeDevices();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
        }

        private async Task SendDataFromSomeDevices()
        {
            var devices = await MyRegistryManager.GetDevicesAsync(Int32.MaxValue);
            try
            {
                var tasks = new List<Task>(devices.Count());
                foreach (var device in devices)
                {
                    tasks.Add(SendDeviceToCloudMessagesAsync(device, device.Id));
                }

                await Task.WhenAll(tasks);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            catch(Exception exc)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Send failed-{0}", exc.Message);
            }
        }

        private async Task SendDeviceToCloudMessagesAsync(Device device, string deviceId)
        {
            string iotHubUri = _connectionstring.Split(';')
                .First(x => x.StartsWith("HostName=", StringComparison.InvariantCultureIgnoreCase))
                .Replace("HostName=", "").Trim();

            if (device == null)
            {
                ServiceEventSource.Current.ServiceMessage(Context, "Device '{0}' doesn't exist.", deviceId);
            }

            var deviceClient = DeviceClient.Create(
                iotHubUri,
                new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, device.Authentication.SymmetricKey.PrimaryKey));

            List<object> events = new List<object>();
            for (int i = 0; i < 10; ++i)
            {
                var body = new
                {
                    Timestamp = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(i))
                };

                events.Add(body);
            }

            Microsoft.Azure.Devices.Client.Message message;
            var serializer = new JsonSerializer();

            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(stream))
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(jsonWriter, events);
                    }
                }

                message = new Microsoft.Azure.Devices.Client.Message(stream.GetBuffer());
                message.Properties.Add("DeviceID", deviceId);
                message.Properties.Add("Temparature", "45");
                message.Properties.Add("FanSpeed", "256");
                message.Properties.Add("IsOnline", "true");
                //message.Properties.Add("GatewayId", "1234"); --> SiteId (RegistrationMessage)

                await deviceClient.SendEventAsync(message);

                ServiceEventSource.Current.ServiceMessage(Context, $"Sent message: {Encoding.UTF8.GetString(stream.GetBuffer())}");
            }
        }

        private async Task RegisterSomeDevices()
        {
            var devices = await MyRegistryManager.GetDevicesAsync(Int32.MaxValue);
            //register 10 devices:
            await AddRandomDevicesAsync(devices.Count(), 10);
        }

        private async Task AddRandomDevicesAsync(int start, int count)
        {
            for (int i = start; i < start + count; ++i)
            {
                await AddDeviceAsync("device" + i);
            }
        }

        private async Task AddDeviceAsync(string deviceId)
        {
            try
            {
                await MyRegistryManager.AddDeviceAsync(new Device(deviceId));
                ServiceEventSource.Current.ServiceMessage(Context, "Added device-{0}", deviceId);
            }
            catch (Microsoft.Azure.Devices.Common.Exceptions.DeviceAlreadyExistsException)
            {
            }
        }

        public Task ToggleCreateDevices()
        {
            _creatingDevices = !_creatingDevices;
            ServiceEventSource.Current.ServiceMessage(Context, "ToggleCreateDevices: {0}", _creatingDevices);
            return Task.FromResult<object>(null);
        }

        public Task ToggleSendData()
        {
            _sendingData = !_sendingData;
            ServiceEventSource.Current.ServiceMessage(Context, "ToggleSendData: {0}", _sendingData);
            return Task.FromResult<object>(null);
        }

        public Task<DeviceEmulatorData> GetStatus()
        {
            return Task.FromResult(new DeviceEmulatorData { CreatingDevices = _creatingDevices, SendingData = _sendingData });
        }
    }
}
