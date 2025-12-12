using Sentinel.CodeQuality.Service.DTOs;

namespace Sentinel.CodeQuality.Service.Publishers;

public interface IReportPublisher
{
    Task PublishFinalResultAsync(ScanFinalResultDto dto);
}
