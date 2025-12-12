namespace Sentinel.CodeQuality.Service.Models;

public class SemgrepRawOutput
{
    public List<SemgrepFinding> Results { get; set; } = new();
}

public class SemgrepFinding
{
    public string CheckId { get; set; }
    public string Path { get; set; }
    public SemgrepExtra Extra { get; set; }
}

public class SemgrepExtra
{
    public string Severity { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
