using System;
using System.Threading.Tasks;

namespace IoTDemoApp.Lib
{
    public interface IDeviceEmulatorService //: IService
    {
        void ToggleCreateDevices();
        void ToggleSendData();

        Task<DeviceEmulatorData> GetStatus();
    }

    public class DeviceEmulatorData
    {
        public bool CreatingDevices { get; set; }
        public bool SendingData { get; set; }
    }

}
