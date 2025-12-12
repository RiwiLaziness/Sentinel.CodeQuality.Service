namespace Sentinel.CodeQuality.Service.DTOs;

public class ScanFinalResultDto
{
    public string ScanId { get; set; }
    public string Status { get; set; } // "PASS" | "FAIL"
    public string? Reason { get; set; }
    public DateTime FinishedAt { get; set; }

    public ScanFinalResultDto() { }

    public ScanFinalResultDto(string scanId, string status, string? reason = null)
    {
        ScanId = scanId;
        Status = status;
        Reason = reason;
        FinishedAt = DateTime.UtcNow;
    }
}
