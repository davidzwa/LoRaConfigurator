using System.Text.Json;

namespace LoraGateway.Services.Contracts;

public abstract class JsonDataStore<T> : IDisposable, IDataStore<T> where T : class, ICloneable
{
    protected T? Store;
    protected StreamWriter? StoreFileStream;

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
        // GetFileStream(path).Write
        Console.WriteLine($"WRITE {path}");
        
        await GetFileStream(path).BaseStream.WriteAsync(serializedBlob, 0, serializedBlob.Length);
        // await File.WriteAllBytesAsync(path, serializedBlob);

        // return Task.CompletedTask;
    }

    protected StreamWriter GetFileStream(string path)
    {
        if (StoreFileStream != null) return StoreFileStream;
        
        StoreFileStream = new StreamWriter(path, false
        //     new FileStreamOptions()
        // {
        //     Access = FileAccess.ReadWrite,
        //     Mode = FileMode.OpenOrCreate,
        //     Share = FileShare.ReadWrite
        // }
        );

        return StoreFileStream;
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
        var blob = await File.ReadAllTextAsync(path);
        Store = JsonSerializer.Deserialize<T>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Store;
    }

    public void Dispose()
    {
        StoreFileStream?.Dispose();
    }
}