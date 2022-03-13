using System.Text.Json;

namespace LoraGateway.Services.Contracts;

public abstract class JsonDataStore<T> : IDisposable, IDataStore<T> where T : class, ICloneable
{
    protected T? Store;
    protected FileStream? StoreFileStream;

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

        var fileStream = GetFileStream(path);
        fileStream.Position = 0;
        await fileStream.WriteAsync(serializedBlob, 0, serializedBlob.Length);
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
        var fileStream = GetFileStream(path);
        var reader = new StreamReader(fileStream, true);
        var blob = await reader.ReadToEndAsync();

        Store = JsonSerializer.Deserialize<T>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Store;
    }

    protected FileStream GetFileStream(string path)
    {
        if (StoreFileStream != null) return StoreFileStream;

        StoreFileStream = new FileStream(path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite
        );

        return StoreFileStream;
    }

    public void Dispose()
    {
        StoreFileStream?.Dispose();
    }
}