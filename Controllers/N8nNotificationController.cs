using Microsoft.AspNetCore.Mvc;
using Sentinel.CodeQuality.Service.DTOs;
using Sentinel.CodeQuality.Service.Models;
using Sentinel.CodeQuality.Service.Publishers;
using Sentinel.CodeQuality.Service.Services;

namespace Sentinel.CodeQuality.Service.Controllers
{
    [ApiController]
    [Route("api/n8n")]
    public class N8nNotificationController : ControllerBase
    {
        private readonly IResultReader _reader;
        private readonly SemgrepMapper _mapper;
        private readonly QualityGateEvaluator _evaluator;
        private readonly IReportPublisher _publisher;
        private readonly ILogger<N8nNotificationController> _logger;
        private readonly string _allowedBasePath;

        public N8nNotificationController(
            IResultReader reader,
            SemgrepMapper mapper,
            QualityGateEvaluator evaluator,
            IReportPublisher publisher,
            IConfiguration configuration,
            ILogger<N8nNotificationController> logger)
        {
            _reader = reader;
            _mapper = mapper;
            _evaluator = evaluator;
            _publisher = publisher;
            _logger = logger;

            // Ruta base donde n8n deposita resultados - importante para evitar path traversal
            _allowedBasePath = configuration["Semgrep:ResultsBasePath"] ?? "/mnt/semgrep/results";
        }

        [HttpPost("semgrep/result-ready")]
        public async Task<IActionResult> OnSemgrepResultReady([FromBody] SemgrepNotificationDto dto)
        {
            if (dto == null)
            {
                _logger.LogWarning("Payload n8n vacío");
                return BadRequest("Payload vacío");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Payload inválido: {@ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(dto.FilePath) || string.IsNullOrWhiteSpace(dto.ScanId))
            {
                _logger.LogWarning("FilePath o ScanId vacíos en la notificación");
                return BadRequest("FilePath y ScanId son obligatorios");
            }

            // Prevent path traversal: ensure requested path is under allowed base path
            var normalizedBase = Path.GetFullPath(_allowedBasePath);
            string normalizedRequested;
            try
            {
                normalizedRequested = Path.GetFullPath(dto.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Path inválido recibido: {FilePath}", dto.FilePath);
                return BadRequest("FilePath inválido");
            }

            if (!normalizedRequested.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Intento de acceso fuera del volumen permitido: {FilePath}", dto.FilePath);
                return BadRequest("FilePath fuera del volumen permitido");
            }

            if (!System.IO.File.Exists(normalizedRequested))
            {
                _logger.LogWarning("Archivo no encontrado: {FilePath}", normalizedRequested);
                return NotFound("Resultado no encontrado");
            }

            try
            {
                _logger.LogInformation("Procesando semgrep result: ScanId={ScanId}, File={File}", dto.ScanId, normalizedRequested);

                // 1. Leer el contenido
                var raw = await _reader.ReadAsync<SemgrepRawOutput>(normalizedRequested);

                // 2. Mapear
                var scanResult = _mapper.Map(raw, dto.ScanId);

                // 3. Evaluar QG
                var finalDecision = _evaluator.Evaluate(scanResult);

                // 4. Publicar (fire-and-forget pero esperamos respuesta de publisher para confirmar)
                await _publisher.PublishFinalResultAsync(finalDecision);

                _logger.LogInformation("Resultado final publicado: ScanId={ScanId}, Status={Status}", finalDecision.ScanId, finalDecision.Status);

                return Accepted(new { scanId = dto.ScanId, status = finalDecision.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando notificación Semgrep para ScanId={ScanId}", dto.ScanId);
                return StatusCode(500, "Ocurrió un error procesando el resultado");
            }
        }
    }
}
