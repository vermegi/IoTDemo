using System.Collections.Generic;

namespace IoTDemo.IoTDeviceActor
{
    public class PropMessage
    {
        public IDictionary<string, object> Properties { get; set; }
        public string DeviceId { get; set; }
    }
}
