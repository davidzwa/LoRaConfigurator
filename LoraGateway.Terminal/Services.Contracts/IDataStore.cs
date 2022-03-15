namespace LoraGateway.Services.Contracts;

public interface IDataStore<T>
{
    public T GetDefaultJson();
    public void WriteStore();
    public Task<T?> LoadStore();
}