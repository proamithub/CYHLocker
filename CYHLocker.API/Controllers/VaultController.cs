using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VaultController : ControllerBase
{
    private readonly IVaultService _vaultService;
    private readonly IPasswordGeneratorService _passwordGenerator;

    public VaultController(IVaultService vaultService, IPasswordGeneratorService passwordGenerator)
    {
        _vaultService = vaultService;
        _passwordGenerator = passwordGenerator;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
        ?? throw new UnauthorizedAccessException("User ID claim not found.");

    // GET: api/vault/items?categoryId=...&q=...&type=...&favoritesOnly=true
    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? categoryId, 
        [FromQuery] string? q, 
        [FromQuery] string? type, 
        [FromQuery] bool favoritesOnly = false)
    {
        var items = await _vaultService.GetVaultItemsAsync(CurrentUserId, categoryId, q, type, favoritesOnly);
        return Ok(new ApiResponse<List<VaultItemDto>> { Success = true, Data = items });
    }

    // GET: api/vault/counts
    [HttpGet("counts")]
    public async Task<IActionResult> GetCounts()
    {
        var counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
        return Ok(new ApiResponse<VaultSidebarCountsDto> { Success = true, Data = counts });
    }

    // GET: api/vault/items/{id}
    [HttpGet("items/{id}")]
    public async Task<IActionResult> GetItemById(string id)
    {
        var item = await _vaultService.GetVaultItemByIdAsync(CurrentUserId, id);
        if (item == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Vault item not found." });

        return Ok(new ApiResponse<VaultItemDto> { Success = true, Data = item });
    }

    // POST: api/vault/items
    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateVaultItemRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _vaultService.CreateVaultItemAsync(CurrentUserId, request);
        return CreatedAtAction(nameof(GetItemById), new { id }, new ApiResponse<string> { Success = true, Message = "Item saved to vault.", Data = id });
    }

    // PUT: api/vault/items/{id}
    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(string id, [FromBody] UpdateVaultItemRequestDto request)
    {
        if (id != request.Id)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Item ID mismatch." });

        var ok = await _vaultService.UpdateVaultItemAsync(CurrentUserId, request);
        if (!ok)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Item not found or cannot be updated." });

        return Ok(new ApiResponse<bool> { Success = true, Message = "Vault item updated successfully.", Data = true });
    }

    // DELETE: api/vault/items/{id}
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var ok = await _vaultService.DeleteVaultItemAsync(CurrentUserId, id);
        if (!ok)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Vault item not found." });

        return Ok(new ApiResponse<bool> { Success = true, Message = "Vault item moved to trash.", Data = true });
    }

    // POST: api/vault/items/{id}/favorite
    [HttpPost("items/{id}/favorite")]
    public async Task<IActionResult> ToggleFavorite(string id)
    {
        var isFav = await _vaultService.ToggleFavoriteAsync(CurrentUserId, id);
        return Ok(new ApiResponse<bool> { Success = true, Data = isFav });
    }

    // ── TRASH MANAGEMENT ───────────────────────────────────────────────────────
    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash()
    {
        var items = await _vaultService.GetTrashItemsAsync(CurrentUserId);
        return Ok(new ApiResponse<List<VaultItemDto>> { Success = true, Data = items });
    }

    [HttpPost("trash/{id}/restore")]
    public async Task<IActionResult> RestoreFromTrash(string id)
    {
        var ok = await _vaultService.RestoreFromTrashAsync(CurrentUserId, id);
        return Ok(new ApiResponse<bool> { Success = ok, Data = ok });
    }

    [HttpDelete("trash/{id}/permanent")]
    public async Task<IActionResult> PermanentDelete(string id)
    {
        var ok = await _vaultService.PermanentlyDeleteItemAsync(CurrentUserId, id);
        return Ok(new ApiResponse<bool> { Success = ok, Data = ok });
    }

    [HttpDelete("trash/empty")]
    public async Task<IActionResult> EmptyTrash()
    {
        var ok = await _vaultService.EmptyTrashAsync(CurrentUserId);
        return Ok(new ApiResponse<bool> { Success = ok, Data = ok });
    }

    // ── AUTOFILL ───────────────────────────────────────────────────────────────
    // GET: api/vault/autofill?domain=github.com
    [HttpGet("autofill")]
    public async Task<IActionResult> AutoFill([FromQuery] string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Domain parameter is required." });

        var matches = await _vaultService.GetAutoFillMatchesAsync(CurrentUserId, domain);
        return Ok(new ApiResponse<List<AutoFillMatchDto>> { Success = true, Data = matches });
    }

    // ── CATEGORIES ─────────────────────────────────────────────────────────────
    // GET: api/vault/categories
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        return Ok(new ApiResponse<List<VaultCategoryDto>> { Success = true, Data = categories });
    }

    // POST: api/vault/categories
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _vaultService.CreateCategoryAsync(CurrentUserId, request);
        return Ok(new ApiResponse<string> { Success = true, Message = "Category created.", Data = id });
    }

    // DELETE: api/vault/categories/{id}
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(string id)
    {
        var ok = await _vaultService.DeleteCategoryAsync(CurrentUserId, id);
        return Ok(new ApiResponse<bool> { Success = ok, Data = ok });
    }

    // ── PASSWORD GENERATOR UTILITY ─────────────────────────────────────────────
    // POST: api/vault/generate-password
    [HttpPost("generate-password")]
    [AllowAnonymous] // Anyone can use the generator utility
    public IActionResult GeneratePassword([FromBody] PasswordGeneratorOptionsDto? options)
    {
        var result = _passwordGenerator.GeneratePassword(options ?? new PasswordGeneratorOptionsDto());
        return Ok(new ApiResponse<GeneratedPasswordDto> { Success = true, Data = result });
    }
}
