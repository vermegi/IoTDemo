namespace IoTDemo.IoTDeviceActor
{
    internal class DeviceStateChangedEvent
    {

        public DeviceStateChangedEvent(string deviceId, string lastState, string message)
        {
            DeviceId = deviceId;
            LastState = lastState;
            Message = message;
        }
        public string DeviceId { get; private set; }

        public string LastState { get; private set; }
        public string Message { get; private set; }
    }
}