using System.Runtime.Serialization;

namespace IoTDemo.IoTDeviceActor
{
    [DataContract]
    public class DeviceStateChangedEvent
    {
        public DeviceStateChangedEvent(string deviceId, string lastState, string message)
        {
            DeviceId = deviceId;
            LastState = lastState;
            Message = message;
        }

        [DataMember]
        public string DeviceId { get; set; }

        [DataMember]
        public string LastState { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}