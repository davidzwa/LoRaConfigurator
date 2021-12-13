using System.Text.Json;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class DeviceDataStore
{
    private DeviceCollection? _store;

    public string GetJsonFilePath()
    {
        return Path.GetFullPath("../../../Data/devices.json", Directory.GetCurrentDirectory());
    }

    private DeviceCollection GetDefaultJson()
    {
        var jsonStore = new DeviceCollection();
        jsonStore.Gateway.Receive.DataRate = 7;
        jsonStore.Gateway.Transmit.DataRate = 7;
        return jsonStore;
    }

    private async Task EnsureSourceExists()
    {
        var path = GetJsonFilePath();
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

        if (!File.Exists(path)) await WriteStore();
    }

    public async Task WriteStore()
    {
        var path = GetJsonFilePath();
        var jsonStore = _store ?? GetDefaultJson();
        var serializedBlob = JsonSerializer.SerializeToUtf8Bytes(jsonStore, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllBytesAsync(path, serializedBlob);
    }

    public Device? GetDevice(string deviceId, bool throwIfNotFound = false)
    {
        var existingDevice = _store?.Devices?.Find(d => d.Id == deviceId);
        if (existingDevice == null && throwIfNotFound)
            throw new InvalidOperationException("Cant update device which isnt registered");
        return existingDevice;
    }

    public Device? GetDeviceByPort(string portName)
    {
        return _store?.Devices?.Find(d => d.LastPortName == portName);
    }

    public async Task<Device?> MarkDeviceGateway(string deviceId)
    {
        var gatewayDevice = GetDevice(deviceId, true);
        gatewayDevice!.IsGateway = true;

        _store?.Devices.ForEach(d =>
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
        if (_store == null) await LoadStore();

        // Ensure device doesnt already exist
        var existingDevice = GetDevice(device.Id);
        if (existingDevice != null) return await UpdateDevice(device.Id, device);

        device.NickName = NameGenerator.GenerateName(10);
        device.RegisteredAt = DateTime.Now.ToFileTimeUtc().ToString();
        _store?.Devices.Add(device);

        await WriteStore();

        return device;
    }

    public async Task LoadStore()
    {
        await EnsureSourceExists();

        var path = GetJsonFilePath();
        var blob = await File.ReadAllTextAsync(path);
        _store = JsonSerializer.Deserialize<DeviceCollection>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}