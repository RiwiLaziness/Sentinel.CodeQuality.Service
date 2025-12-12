namespace Sentinel.CodeQuality.Service.Models;

public class Finding
{
    public string Rule { get; set; }
    public string File { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
}
