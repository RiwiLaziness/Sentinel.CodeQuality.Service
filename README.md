# 📊 Sentinel Code Quality Service

**Sentinel Code Quality Service** is a specialized microservice responsible for evaluating code quality and enforcing quality gates based on Semgrep static analysis results. It processes Semgrep scan outputs, applies configurable quality gate rules, and publishes pass/fail decisions to RabbitMQ for downstream actions.

---

## 📋 Table of Contents

- [Features](#-features)
- [Architecture](#-architecture)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Usage](#-usage)
- [API Endpoints](#-api-endpoints)
- [Quality Gate Rules](#-quality-gate-rules)
- [Data Structures](#-data-structures)
- [RabbitMQ Integration](#-rabbitmq-integration)
- [File Reading](#-file-reading)
- [Troubleshooting](#-troubleshooting)

---

## ✨ Features

- ✅ **Semgrep Integration**: Processes Semgrep static analysis results
- ✅ **Quality Gate Evaluation**: Enforces configurable pass/fail criteria
- ✅ **Secret Detection**: Automatically fails scans with exposed secrets
- ✅ **Severity-Based Rules**: Evaluates findings by severity levels (ERROR, WARNING, INFO)
- ✅ **RabbitMQ Publisher**: Publishes quality gate decisions with automatic retry
- ✅ **Secure File Access**: Reads scan results from mounted volumes with path validation
- ✅ **Result Normalization**: Converts Semgrep format to unified finding structure
- ✅ **Audit Trail**: Includes timestamps and reasons for quality gate decisions

---

## 🏗️ Architecture

```
┌──────────┐      ┌─────────────────────┐      ┌─────────────┐
│   n8n    │─────▶│  Code Quality       │─────▶│  RabbitMQ   │
│ Workflow │      │  Service (.NET)     │      │  Exchange   │
└──────────┘      └─────────────────────┘      └─────────────┘
     │                      │                          │
     │                      ▼                          ▼
     │            ┌──────────────────┐      ┌──────────────────┐
     │            │  Semgrep Result  │      │  Downstream      │
     └───────────▶│  JSON File       │      │  Actions:        │
                  │  (Volume)        │      │  - Block Deploy  │
                  └──────────────────┘      │  - Notify Team   │
                                            │  - Create Ticket │
                                            └──────────────────┘
```

### Workflow

1. **n8n** executes Semgrep static analysis
2. **n8n** saves Semgrep results to shared volume
3. **n8n** sends notification → `POST /api/n8n/semgrep/result-ready`
4. **Code Quality Service** reads and parses Semgrep JSON
5. **Code Quality Service** maps findings to internal structure
6. **Code Quality Service** evaluates quality gate rules
7. **Code Quality Service** publishes PASS/FAIL decision to RabbitMQ
8. **Downstream services** take action based on decision

---

## 📦 Prerequisites

- **.NET 8.0 SDK** or higher
- **RabbitMQ** (localhost:5672 or remote server)
- **Semgrep** CLI tool (for generating scan results)
- **Shared Volume** or file system access to Semgrep outputs
- **(Optional)** Docker for containerized deployment

---

## 🚀 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/your-company/sentinel-codequality-service.git
cd sentinel-codequality-service
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

### 4. Run the Service

```bash
dotnet run
```

The service will be available at:
- **HTTP**: `http://localhost:5220`
- **HTTPS**: `https://localhost:7144`
- **Swagger UI**: `http://localhost:5220/swagger`

---

## ⚙️ Configuration

### `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "Exchange": "scan.results",
    "RoutingKey": "scan.result.final"
  },
  "Semgrep": {
    "ResultsBasePath": "/mnt/semgrep/results"
  },
  "QualityGate": {
    "MaxCriticalFindings": 5,
    "FailOnSecrets": true,
    "BlockedSeverities": ["ERROR"]
  }
}
```

### Environment Variables (Production)

```bash
export RabbitMQ__Host="rabbitmq.prod.com"
export RabbitMQ__Username="sentinel"
export RabbitMQ__Password="super-secret-password"
export Semgrep__ResultsBasePath="/var/semgrep/results"
```

---

## 📖 Usage

### Send Semgrep Result Notification (from n8n)

```bash
curl -X POST http://localhost:5220/api/n8n/semgrep/result-ready \
  -H "Content-Type: application/json" \
  -d '{
    "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "filePath": "/mnt/semgrep/results/scan-123.json",
    "repository": "github.com/myorg/myapp",
    "branch": "main",
    "triggeredBy": "ci-pipeline",
    "timestamp": "2025-12-10T15:30:00Z"
  }'
```

**Response (HTTP 202 Accepted):**

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "PASS"
}
```

**Or (HTTP 202 Accepted with FAIL):**

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "FAIL"
}
```

---

## 🔌 API Endpoints

### Process Semgrep Results

```http
POST /api/n8n/semgrep/result-ready
Content-Type: application/json
```

**Request Body:**

```json
{
  "scanId": "unique-scan-id",
  "filePath": "/mnt/semgrep/results/scan-123.json",
  "repository": "github.com/myorg/myapp",
  "branch": "main",
  "triggeredBy": "webhook",
  "timestamp": "2025-12-10T15:30:00Z",
  "metadata": {
    "commitHash": "abc123def456",
    "author": "john.doe@company.com"
  }
}
```

**Parameters:**
- `scanId` (string, required): Unique identifier for the scan
- `filePath` (string, required): Absolute path to Semgrep JSON result file
- `repository` (string, required): Repository URL or name
- `branch` (string, required): Git branch that was scanned
- `triggeredBy` (string, required): Source of scan trigger (ci-pipeline, webhook, manual)
- `timestamp` (DateTime, optional): When n8n sent the notification
- `metadata` (object, optional): Additional contextual information

**Response (Success):**

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "PASS"
}
```

**Response (Quality Gate Failure):**

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "FAIL"
}
```

**Error Responses:**

- `400 Bad Request`: Invalid payload or file path outside allowed volume
- `404 Not Found`: Semgrep result file doesn't exist
- `500 Internal Server Error`: Processing or RabbitMQ publishing failure

---

## 🚦 Quality Gate Rules

### Default Rules

The quality gate evaluator applies the following default rules:

#### Rule 1: Secret Detection (CRITICAL)

```csharp
if (secrets > 0)
    return FAIL("Exposed secrets detected")
```

**Rationale**: Any exposed secret (API keys, passwords, tokens) is an immediate security risk.

---

#### Rule 2: Critical Finding Threshold

```csharp
if (criticalFindings > 5)
    return FAIL("Too many critical findings")
```

**Rationale**: More than 5 ERROR-severity findings indicates significant code quality issues.

---

#### Rule 3: All Checks Pass

```csharp
if (secrets == 0 && criticalFindings <= 5)
    return PASS()
```

**Rationale**: Code meets minimum quality standards.

---

### Customizing Quality Gate Rules

Edit `Services/QualityGateEvaluator.cs` to customize rules:

```csharp
public class QualityGateEvaluator
{
    private readonly IConfiguration _config;

    public ScanFinalResultDto Evaluate(ScanResult scan)
    {
        var maxCritical = _config.GetValue<int>("QualityGate:MaxCriticalFindings", 5);
        var failOnSecrets = _config.GetValue<bool>("QualityGate:FailOnSecrets", true);
        
        var critical = scan.Findings.Count(f => f.Severity == "ERROR");
        var secrets = scan.Findings.Count(f => f.Rule.Contains("secret"));
        var warnings = scan.Findings.Count(f => f.Severity == "WARNING");

        // Custom rule: Fail if too many warnings
        if (warnings > 20)
            return Fail(scan.ScanId, "Excessive warnings detected");

        if (failOnSecrets && secrets > 0)
            return Fail(scan.ScanId, "Exposed secrets detected");

        if (critical > maxCritical)
            return Fail(scan.ScanId, $"Critical findings exceed threshold ({critical} > {maxCritical})");

        return Pass(scan.ScanId);
    }
}
```

---

## 📊 Data Structures

### SemgrepNotificationDto (Input)

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "filePath": "/mnt/semgrep/results/scan-123.json",
  "repository": "github.com/myorg/myapp",
  "branch": "main",
  "triggeredBy": "ci-pipeline",
  "timestamp": "2025-12-10T15:30:00Z",
  "metadata": {
    "commitHash": "abc123",
    "author": "developer@company.com"
  }
}
```

---

### SemgrepRawOutput (Semgrep JSON Format)

```json
{
  "results": [
    {
      "check_id": "python.lang.security.audit.hardcoded-password",
      "path": "src/config.py",
      "extra": {
        "severity": "ERROR",
        "metadata": {
          "category": "security",
          "confidence": "HIGH",
          "cwe": "CWE-798",
          "owasp": "A07:2021"
        }
      }
    }
  ]
}
```

---

### Finding (Internal Normalized Format)

```csharp
public class Finding
{
    public string Rule { get; set; }        // Check ID
    public string File { get; set; }        // File path
    public string Severity { get; set; }    // ERROR, WARNING, INFO
    public string Description { get; set; } // From metadata
}
```

---

### ScanFinalResultDto (Output to RabbitMQ)

```json
{
  "scanId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "FAIL",
  "reason": "Exposed secrets detected",
  "finishedAt": "2025-12-10T15:35:00.000Z"
}
```

**Fields:**
- `scanId`: Unique scan identifier
- `status`: `"PASS"` or `"FAIL"`
- `reason`: Explanation for FAIL status (null for PASS)
- `finishedAt`: ISO 8601 timestamp of evaluation

---

## 🐰 RabbitMQ Integration

### Exchange Configuration

- **Exchange Name**: `scan.results` (configurable)
- **Exchange Type**: Topic
- **Durable**: Yes
- **Routing Key**: `scan.result.final` (configurable)

### Message Format

Published messages are JSON-serialized `ScanFinalResultDto` objects.

### Publisher Features

- **Automatic Retry**: Up to 3 attempts with exponential backoff
- **Connection Recovery**: Automatically recreates failed connections
- **Publisher Confirms**: Waits for broker acknowledgment (5-second timeout)
- **Persistent Messages**: Delivery mode = 2 (survives broker restarts)

### Example Consumer (Node.js)

```javascript
const amqp = require('amqplib');

async function consumeResults() {
  const connection = await amqp.connect('amqp://localhost');
  const channel = await connection.createChannel();
  
  await channel.assertQueue('quality-gate-results');
  await channel.bindQueue('quality-gate-results', 'scan.results', 'scan.result.final');
  
  channel.consume('quality-gate-results', (msg) => {
    const result = JSON.parse(msg.content.toString());
    console.log(`Scan ${result.scanId}: ${result.status}`);
    
    if (result.status === 'FAIL') {
      console.log(`Reason: ${result.reason}`);
      // Block deployment, send notification, etc.
    }
    
    channel.ack(msg);
  });
}

consumeResults();
```

---

## 📁 File Reading

### Volume Configuration

Configure the base path for Semgrep results in `appsettings.json`:

```json
{
  "Semgrep": {
    "ResultsBasePath": "/mnt/semgrep/results"
  }
}
```

### Security Features

- **Path Traversal Prevention**: Validates all file paths against configured base
- **Absolute Path Resolution**: Normalizes paths to prevent directory traversal attacks
- **Read-Only Access**: Opens files with read-only permissions
- **Existence Validation**: Checks file existence before attempting read

### Example: Path Validation

```csharp
var normalizedBase = Path.GetFullPath("/mnt/semgrep/results");
var normalizedRequested = Path.GetFullPath(dto.FilePath);

if (!normalizedRequested.StartsWith(normalizedBase))
{
    return BadRequest("FilePath outside allowed volume");
}
```

This prevents attacks like:
- `../../../etc/passwd`
- `/etc/shadow`
- `C:\Windows\System32\config\SAM`

---

## 🔄 Processing Pipeline

### Step-by-Step Flow

```
1. Receive Notification
   ↓
2. Validate Request (scanId, filePath)
   ↓
3. Security Check (path traversal prevention)
   ↓
4. File Existence Verification
   ↓
5. Read Semgrep JSON
   ↓
6. Parse & Deserialize
   ↓
7. Map to Internal Finding Structure
   ↓
8. Evaluate Quality Gate Rules
   ↓
9. Generate Pass/Fail Decision
   ↓
10. Publish to RabbitMQ
   ↓
11. Return HTTP 202 Response
```

### Error Handling

Each step includes comprehensive error handling:

- **Validation errors** → `400 Bad Request`
- **File not found** → `404 Not Found`
- **JSON parsing errors** → `500 Internal Server Error`
- **RabbitMQ failures** → Automatic retry (3 attempts)

---

## 🐛 Troubleshooting

### Error: "Payload vacío"

**Cause**: Request body is null or empty

**Solution**:
1. Verify Content-Type header: `application/json`
2. Ensure request body is valid JSON
3. Check n8n HTTP Request node configuration

---

### Error: "FilePath y ScanId son obligatorios"

**Cause**: Missing required fields in request

**Solution**:
1. Include both `filePath` and `scanId` in payload
2. Verify fields are not empty strings
3. Check n8n variable interpolation

---

### Error: "FilePath inválido"

**Cause**: Malformed file path

**Solution**:
1. Use absolute paths: `/mnt/semgrep/results/scan.json`
2. Avoid special characters in filenames
3. Check for null bytes or invalid characters

---

### Error: "FilePath fuera del volumen permitido"

**Cause**: Security check prevents reading files outside base path

**Solution**:
1. Verify `Semgrep:ResultsBasePath` in `appsettings.json`
2. Ensure Semgrep writes results to correct volume
3. Check for symlinks that might bypass security
4. Use paths within configured base directory

---

### Error: "Resultado no encontrado"

**Cause**: Semgrep result file doesn't exist at specified path

**Solution**:
1. Verify Semgrep execution completed successfully
2. Check file path in n8n workflow
3. Confirm volume mount in Docker/Kubernetes
4. List directory contents: `ls -la /mnt/semgrep/results/`

---

### Error: "Archivo vacío o inválido"

**Cause**: JSON deserialization failed

**Solution**:
1. Validate JSON syntax: `jq . /path/to/result.json`
2. Ensure Semgrep output format is JSON: `semgrep --json`
3. Check for incomplete file writes (file size = 0)
4. Verify UTF-8 encoding

---

### RabbitMQ Connection Issues

**Symptoms**: Logs show "RabbitMQ: intentando conectar"

**Solution**:
1. Verify RabbitMQ is running: `docker ps | grep rabbitmq`
2. Test connectivity: `telnet rabbitmq 5672`
3. Check credentials in `appsettings.json`
4. Review RabbitMQ logs for authentication errors
5. Ensure exchange is declared (auto-created on first publish)

---

### Quality Gate Always Passes/Fails

**Cause**: Misconfigured evaluation rules

**Solution**:
1. Add debug logging in `QualityGateEvaluator.cs`
2. Check severity mapping from Semgrep
3. Verify rule name patterns (e.g., "secret" substring)
4. Review finding counts in logs
5. Test with known failing/passing scan results

---

## 📝 Logs

### Log Levels

Configure in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Sentinel.CodeQuality.Service": "Debug",
      "Sentinel.CodeQuality.Service.Publishers": "Debug"
    }
  }
}
```

### Important Log Messages

| Log Message | Meaning |
|-------------|---------|
| `"Procesando semgrep result: ScanId={ScanId}"` | Processing started |
| `"Resultado final publicado: ScanId={ScanId}, Status={Status}"` | Quality gate decision made |
| `"RabbitMQ: conexión establecida"` | Successfully connected to RabbitMQ |
| `"Publicado resultado del scan {ScanId}"` | Message published to RabbitMQ |
| `"Intento de acceso fuera del volumen permitido"` | Security violation detected |
| `"Archivo no encontrado: {FilePath}"` | File doesn't exist |

---

## 🚢 Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5220

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Sentinel.CodeQuality.Service.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sentinel.CodeQuality.Service.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: sentinel
      RABBITMQ_DEFAULT_PASS: sentinel123

  codequality-service:
    build: .
    ports:
      - "5220:5220"
    volumes:
      - ./semgrep-results:/mnt/semgrep/results:ro
    depends_on:
      - rabbitmq
    environment:
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Username: sentinel
      RabbitMQ__Password: sentinel123
      Semgrep__ResultsBasePath: /mnt/semgrep/results
```

Run:

```bash
docker-compose up -d
```

---

## 🔗 Integration with n8n

### Complete n8n Workflow Example

```json
{
  "nodes": [
    {
      "name": "Clone Repository",
      "type": "n8n-nodes-base.executeCommand",
      "parameters": {
        "command": "git clone {{ $json.repoUrl }} /tmp/scan-{{ $json.scanId }}"
      }
    },
    {
      "name": "Run Semgrep",
      "type": "n8n-nodes-base.executeCommand",
      "parameters": {
        "command": "semgrep --config=auto --json --output=/mnt/semgrep/results/{{ $json.scanId }}.json /tmp/scan-{{ $json.scanId }}"
      }
    },
    {
      "name": "Notify Code Quality Service",
      "type": "n8n-nodes-base.httpRequest",
      "parameters": {
        "method": "POST",
        "url": "http://codequality-service:5220/api/n8n/semgrep/result-ready",
        "bodyParameters": {
          "scanId": "={{ $json.scanId }}",
          "filePath": "=/mnt/semgrep/results/{{ $json.scanId }}.json",
          "repository": "={{ $json.repoUrl }}",
          "branch": "={{ $json.branch }}",
          "triggeredBy": "n8n-workflow"
        }
      }
    },
    {
      "name": "Check Quality Gate",
      "type": "n8n-nodes-base.if",
      "parameters": {
        "conditions": {
          "string": [
            {
              "value1": "={{ $json.status }}",
              "value2": "PASS"
            }
          ]
        }
      }
    }
  ]
}
```

---

## 🧪 Testing

### Manual Test with curl

```bash
# 1. Generate Semgrep result
semgrep --config=auto --json --output=/tmp/test-scan.json /path/to/code

# 2. Send notification
curl -X POST http://localhost:5220/api/n8n/semgrep/result-ready \
  -H "Content-Type: application/json" \
  -d '{
    "scanId": "test-123",
    "filePath": "/tmp/test-scan.json",
    "repository": "test-repo",
    "branch": "main",
    "triggeredBy": "manual"
  }'

# 3. Verify response
# Expected: HTTP 202 with {"scanId":"test-123","status":"PASS"} or "FAIL"
```

---

## 🤝 Contributing

1. Fork the project
2. Create your feature branch (`git checkout -b feature/new-rule`)
3. Commit your changes (`git commit -m 'Add new quality gate rule'`)
4. Push to the branch (`git push origin feature/new-rule`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License. See `LICENSE` file for details.

---

## 👥 Team

- **Lead Developer**: [Your Name]
- **Software Architect**: [Name]
- **Security Engineer**: [Name]

---

## 📞 Support

- **Email**: support@sentinel.com
- **Slack**: #sentinel-code-quality
- **Jira**: [SENTINEL Project](https://jira.company.com/projects/SENTINEL)

---

## 🔗 Useful Links

- [Semgrep Documentation](https://semgrep.dev/docs/)
- [Semgrep Rules Registry](https://semgrep.dev/explore)
- [RabbitMQ Tutorials](https://www.rabbitmq.com/getstarted.html)
- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/)

---

**Thank you for using Sentinel Code Quality Service!** 📊🛡️
