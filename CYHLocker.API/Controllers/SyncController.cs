using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("User ID claim not found.");

    // POST: api/sync/register-device
    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "DeviceId is required." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _syncService.RegisterDeviceAsync(CurrentUserId, request, clientIp);
        return Ok(new ApiResponse<DeviceRegistrationResponseDto> { Success = true, Message = "Device registered.", Data = result });
    }

    // POST: api/sync/pull
    [HttpPost("pull")]
    public async Task<IActionResult> PullChanges([FromBody] SyncPullRequestDto request)
    {
        var result = await _syncService.PullChangesAsync(CurrentUserId, request);
        return Ok(new ApiResponse<SyncPullResponseDto> { Success = true, Data = result });
    }

    // GET: api/sync/pull?sinceUtc=...&deviceId=...
    [HttpGet("pull")]
    public async Task<IActionResult> PullChangesGet([FromQuery] DateTime? sinceUtc, [FromQuery] string? deviceId)
    {
        var request = new SyncPullRequestDto { SinceUtc = sinceUtc, DeviceId = deviceId };
        var result = await _syncService.PullChangesAsync(CurrentUserId, request);
        return Ok(new ApiResponse<SyncPullResponseDto> { Success = true, Data = result });
    }

    // POST: api/sync/push
    [HttpPost("push")]
    public async Task<IActionResult> PushChanges([FromBody] SyncPushRequestDto request)
    {
        var result = await _syncService.PushChangesAsync(CurrentUserId, request);
        return Ok(new ApiResponse<SyncPushResponseDto> { Success = result.Success, Message = $"Processed {result.ProcessedCount} items.", Data = result });
    }

    // GET: api/sync/status
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _syncService.GetSyncStatusAsync(CurrentUserId);
        return Ok(new ApiResponse<SyncStatusDto> { Success = true, Data = result });
    }
}
