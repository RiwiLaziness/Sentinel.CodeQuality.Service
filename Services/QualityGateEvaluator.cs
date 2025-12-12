using Sentinel.CodeQuality.Service.DTOs;
using Sentinel.CodeQuality.Service.Models;

namespace Sentinel.CodeQuality.Service.Services;

public class QualityGateEvaluator
{
    public ScanFinalResultDto Evaluate(ScanResult scan)
    {
        var critical = scan.Findings.Count(f => f.Severity == "ERROR");
        var secrets = scan.Findings.Count(f => f.Rule.Contains("secret"));

        if (secrets > 0)
            return Fail(scan.ScanId, "Se detectaron secretos expuestos.");

        if (critical > 5)
            return Fail(scan.ScanId, "Demasiados hallazgos críticos.");

        return Pass(scan.ScanId);
    }

    private ScanFinalResultDto Pass(string scanId) =>
        new(scanId, "PASS");

    private ScanFinalResultDto Fail(string scanId, string reason) =>
        new(scanId, "FAIL", reason);
}
