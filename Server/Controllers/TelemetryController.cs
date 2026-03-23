using Microsoft.AspNetCore.Mvc;

namespace OpenRocketArena.Server.Controllers;

/// <summary>
/// Mango Telemetry endpoints
/// </summary>
[ApiController]
public class TelemetryController : ControllerBase
{
    [HttpPost("/telemetry/{productId}/{eventType}/{eventLevel}/{eventId}")]
    public IActionResult PostEvent(int productId, int eventType, int eventLevel, string eventId)
    {
        // Swallow the telemetry we have no need for it but prevents game from spamming errors.
        return NoContent();
    }
}
