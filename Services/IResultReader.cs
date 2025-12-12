namespace Sentinel.CodeQuality.Service.Services;

public interface IResultReader
{
    Task<T> ReadAsync<T>(string path);
}
