namespace LoraGateway.Services.Contracts;

public interface IDataStore<T>
{
    public T GetDefaultJson();
    public Task WriteStore();
    public Task<T> LoadStore();
}