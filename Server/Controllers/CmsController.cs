using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// CMS endpoints
/// </summary>
[ApiController]
public class CmsController(IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("/publish/{environment}")]
    public IActionResult GetStage(string environment)
    {
        var version = config[$"Cms:Versions:{environment}"] ?? "1.default";
        return Ok(new { version });
    }

    [HttpGet("/cms/documents/{version}/{documentId}")]
    public IActionResult GetDocument(string version, string documentId)
    {
        // Strip .gz suffix to get the actual file name
        var fileName = documentId.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? documentId[..^3] : documentId;

        var filePath = Path.Combine(env.WebRootPath, "cms", version, fileName);
        if (!System.IO.File.Exists(filePath)) return NotFound(new { error = "document_not_found" });

        var jsonBytes = System.IO.File.ReadAllBytes(filePath);

        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(jsonBytes);
        }

        Response.Headers.ContentEncoding = "gzip";
        return File(compressedStream.ToArray(), "application/json");
    }
}
