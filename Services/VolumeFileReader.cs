using System.Text.Json;

namespace Sentinel.CodeQuality.Service.Services;

public class VolumeFileReader : IResultReader
{
    public async Task<T> ReadAsync<T>(string path)
    {
        using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(fs)
               ?? throw new InvalidOperationException("Archivo vacío o inválido");
    }
}
