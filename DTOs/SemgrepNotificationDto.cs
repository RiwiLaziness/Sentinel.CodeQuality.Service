namespace Sentinel.CodeQuality.Service.DTOs;

using System;

public class SemgrepNotificationDto
{
    /// Identificador único del escaneo (viene desde n8n).
    public string ScanId { get; set; }
    
    /// Ruta al JSON generado por Semgrep dentro del volumen compartido.
    public string FilePath { get; set; }

    /// Nombre del repositorio analizado.
    public string Repository { get; set; }

    /// Rama objetivo del análisis.
    public string Branch { get; set; }

    /// Origen del análisis (usuario, webhook, CI/CD).
    public string TriggeredBy { get; set; }

    /// Momento en el que n8n envió la notificación.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// Información extra opcional enviada por n8n.
    public Dictionary<string, string>? Metadata { get; set; }
}
