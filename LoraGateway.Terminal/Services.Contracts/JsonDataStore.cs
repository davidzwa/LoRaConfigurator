using System.Text.Json;

namespace LoraGateway.Services.Contracts;

public abstract class JsonDataStore<T> : IDataStore<T> where T : class, ICloneable
{
    protected T? Store;

    public abstract T GetDefaultJson();

    public abstract string GetJsonFileName();

    protected string GetDataFolder()
    {
        return DataStoreExtensions.BasePath;
    }
    
    protected string GetDataFolderFullPath()
    {
        var folder = GetDataFolder();
        return Path.GetFullPath(folder, Directory.GetCurrentDirectory());
    }
    
    public string GetJsonFilePath()
    {
        var fileName = GetJsonFileName();
        var folder = GetDataFolderFullPath();
        return Path.Join(folder, fileName);
    }

    protected Task EnsureSourceExists()
    {
        var path = GetJsonFilePath();
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return Task.CompletedTask;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

        if (!File.Exists(path))
        {
            WriteStore();
        }

        return Task.CompletedTask;
    }

    public T? GetStore()
    {
        if (Store != null) return Store.Clone() as T;

        return null;
    }

    public async Task<T?> LoadStore()
    {
        await EnsureSourceExists();

        var path = GetJsonFilePath();

        var blob = await File.ReadAllBytesAsync(path);

        Store = JsonSerializer.Deserialize<T>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Store;
    }
    
    public void WriteStore()
    {
        var path = GetJsonFilePath();
        var jsonStore = Store ?? GetDefaultJson();
        var serializedBlob = JsonSerializer.SerializeToUtf8Bytes(jsonStore, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllBytes(path, serializedBlob);
    }
}