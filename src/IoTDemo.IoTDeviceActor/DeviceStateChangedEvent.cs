namespace IoTDemo.IoTDeviceActor
{
    internal class DeviceStateChangedEvent
    {

        public DeviceStateChangedEvent(string lastState, string message)
        {
            LastState = lastState;
            Message = message;
        }

        public string LastState { get; private set; }
        public string Message { get; private set; }
    }
}