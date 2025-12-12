namespace Sentinel.CodeQuality.Service.Models;

public class ScanResult
{
    public string ScanId { get; set; }
    public List<Finding> Findings { get; set; } = new();
}
