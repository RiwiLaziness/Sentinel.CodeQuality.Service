using Sentinel.CodeQuality.Service.Models;

namespace Sentinel.CodeQuality.Service.Services;

public class SemgrepMapper
{
    public ScanResult Map(SemgrepRawOutput raw, string scanId)
    {
        var result = new ScanResult { ScanId = scanId };

        result.Findings = raw.Results.Select(r => new Finding
        {
            Rule = r.CheckId,
            File = r.Path,
            Severity = r.Extra?.Severity,
            Description = r.Extra?.Metadata?.FirstOrDefault().Value
        }).ToList();

        return result;
    }
}
