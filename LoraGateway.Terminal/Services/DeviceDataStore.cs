using System.Text.Json;
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
        var existingDevice = Store?.Devices?.Find(d => d.Id == deviceId);
        if (existingDevice == null && throwIfNotFound)
            throw new InvalidOperationException("Cant update device which isnt registered");
        return existingDevice;
    }

    public Device? GetDeviceByPort(string portName)
    {
        return Store?.Devices?.Find(d => d?.LastPortName == portName);
    }

    public async Task<Device?> MarkDeviceGateway(string deviceId)
    {
        var gatewayDevice = GetDevice(deviceId, true);
        gatewayDevice!.IsGateway = true;

        Store?.Devices.ForEach(d =>
        {
            if (!gatewayDevice.Id.Equals(deviceId)) d.IsGateway = false;
        });

        await WriteStore();

        return gatewayDevice;
    }

    public async Task<Device?> UpdateDevice(string deviceId, Device newDevice)
    {
        var existingDevice = GetDevice(deviceId, true);
        existingDevice!.Meta = newDevice.Meta;
        existingDevice.FirmwareVersion = newDevice.FirmwareVersion;
        existingDevice.LastPortName = newDevice.LastPortName;
        existingDevice.IsGateway = newDevice.IsGateway;

        await WriteStore();
        return existingDevice;
    }

    public async Task<Device?> GetOrAddDevice(Device device)
    {
        if (Store == null) await LoadStore();

        // Ensure device doesnt already exist
        var existingDevice = GetDevice(device.Id);
        if (existingDevice != null) return await UpdateDevice(device.Id, device);

        device.NickName = NameGenerator.GenerateName(10);
        device.RegisteredAt = DateTime.Now.ToFileTimeUtc().ToString();
        Store?.Devices.Add(device);

        await WriteStore();

        return device;
    }
}