﻿using System.Text.Json;
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

    private async Task EnsureSourceExists(string path)
    {
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

        if (!File.Exists(path)) await WriteStore(path);
    }

    public async Task WriteStore(string path)
    {
        var jsonStore = _store ?? GetDefaultJson();
        var serializedBlob = JsonSerializer.SerializeToUtf8Bytes(jsonStore, new JsonSerializerOptions()
        {
            WriteIndented = true
        });

        var stream = File.OpenWrite(path);
        await stream.WriteAsync(serializedBlob);
        stream.Close();
    }

    public Device? GetDevice(string deviceId)
    {
        return _store?.Devices?.Find(d => d.Id == deviceId);
    }
    
    public async Task<Device> GetOrAddDevice(Device device)
    {
        if (_store == null) await LoadStore();

        // Ensure device doesnt already exist
        var existingDevice = GetDevice(device.Id);
        if (existingDevice != null) return existingDevice;

        device.NickName = NameGenerator.GenerateName(10);
        device.RegisteredAt = DateTime.Now.ToFileTimeUtc().ToString();
        _store?.Devices.Add(device);

        var path = GetJsonFilePath();
        await WriteStore(path);

        return device;
    }

    public async Task LoadStore()
    {
        var path = GetJsonFilePath();
        await EnsureSourceExists(path);

        var blob = await File.ReadAllTextAsync(path);
        _store = JsonSerializer.Deserialize<DeviceCollection>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}