using System.Text.Json;

namespace LoraGateway.Services.Contracts;

public abstract class JsonDataStore<T> : IDataStore<T>
{
    protected T? Store;
    
    public abstract T GetDefaultJson();

    public abstract string GetJsonFileName();
    
    public string GetJsonFilePath()
    {
        var fileName = GetJsonFileName();
        var fullJsonStorePath = Path.Join(JsonDataStoreExtensions.BasePath, fileName);
        return Path.GetFullPath(fullJsonStorePath, Directory.GetCurrentDirectory());
    }
    
    protected async Task EnsureSourceExists()
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
        var jsonStore = Store ?? GetDefaultJson();
        var serializedBlob = JsonSerializer.SerializeToUtf8Bytes(jsonStore, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllBytesAsync(path, serializedBlob);
    }

    public async Task<T> LoadStore()
    {
        await EnsureSourceExists();

        var path = GetJsonFilePath();
        var blob = await File.ReadAllTextAsync(path);
        Store = JsonSerializer.Deserialize<T>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Store!;
    }
}