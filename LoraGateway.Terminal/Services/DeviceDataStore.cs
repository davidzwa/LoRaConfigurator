using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class DeviceDataStore : JsonDataStore<DeviceCollection>
{
    public override string GetJsonFileName()
    {
        return "devices.json";
    }

    public override DeviceCollection GetDefaultJson()
    {
        var jsonStore = new DeviceCollection();
        jsonStore.Gateway.Receive.DataRate = 7;
        jsonStore.Gateway.Transmit.DataRate = 7;
        return jsonStore;
    }

    public Device? GetDevice(string deviceId, bool throwIfNotFound = false)
    {
        var existingDevice = Store?.Devices?.Find(d => d.HardwareId == deviceId);
        if (existingDevice == null && throwIfNotFound)
            throw new InvalidOperationException("Cant update device which isnt registered");
        return existingDevice;
    }

    public Device? GetDeviceByNick(string nickName, bool throwIfNotFound = false)
    {
        var existingDevice =
            Store?.Devices?.Find(d => d.NickName.Equals(nickName, StringComparison.InvariantCultureIgnoreCase));
        if (existingDevice == null && throwIfNotFound)
            throw new InvalidOperationException($"Could not find device by nickname '{nickName}'");
        return existingDevice;
    }

    public IEnumerable<Device?> GetDeviceByPort(string portName)
    {
        if (Store?.Devices == null)
            return new List<Device>();

        return Store.Devices.FindAll(d => d?.LastPortName == portName);
    }

    public Task<Device> UpdateDevice(string deviceId, Device newDevice)
    {
        var existingDevice = GetDevice(deviceId, true);
        existingDevice!.Meta = newDevice.Meta;
        existingDevice.FirmwareVersion = newDevice.FirmwareVersion;
        existingDevice.LastPortName = newDevice.LastPortName;

        WriteStore();
        return Task.FromResult(existingDevice);
    }

    public async Task<Device?> GetOrAddDevice(Device device)
    {
        if (Store == null)
        {
            await LoadStore();
        }

        // Ensure device doesnt already exist
        var existingDevice = GetDevice(device.HardwareId);
        if (existingDevice != null) return await UpdateDevice(device.HardwareId, device);

        device.NickName = NameGenerator.GenerateName(10);
        device.RegisteredAt = DateTime.Now.ToFileTimeUtc().ToString();
        Store?.Devices.Add(device);

        WriteStore();

        return device;
    }
}