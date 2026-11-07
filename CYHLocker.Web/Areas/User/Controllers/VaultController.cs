using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User,Admin")]
public class VaultController : Controller
{
    private readonly IVaultService _vaultService;
    private readonly IPasswordGeneratorService _passGen;

    public VaultController(IVaultService vaultService, IPasswordGeneratorService passGen)
    {
        _vaultService = vaultService;
        _passGen = passGen;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string CurrentUserName => User.Identity?.Name ?? "User";
    private string CurrentUserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool IsMfa => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");

    // GET: /User/Vault?categoryId=...&q=...&type=...&favoritesOnly=...
    public async Task<IActionResult> Index(string? categoryId, string? q, string? type, bool favoritesOnly = false)
    {
        SetCommonViewBag("Vault");
        ViewBag.SelectedCategory = categoryId;
        ViewBag.SearchQuery = q;
        ViewBag.SelectedType = string.IsNullOrEmpty(type) ? "All" : type;
        ViewBag.FavoritesOnly = favoritesOnly;

        var items = await _vaultService.GetVaultItemsAsync(CurrentUserId, categoryId, q, type, favoritesOnly);
        var categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        var counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);

        ViewBag.Categories = categories;
        ViewBag.Counts = counts;
        return View(items);
    }

    // AJAX: /User/Vault/GetItemDetailsJson?id=... (for Right Slide-out Drawer)
    [HttpGet]
    public async Task<IActionResult> GetItemDetailsJson(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        var item = await _vaultService.GetVaultItemByIdAsync(CurrentUserId, id);
        if (item == null) return NotFound();
        return Json(new { success = true, data = item });
    }

    // GET: /User/Vault/Create?type=Login&categoryId=...
    [HttpGet]
    public async Task<IActionResult> Create(string? type, string? categoryId)
    {
        SetCommonViewBag("Vault");
        ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
        
        var model = new CreateVaultItemRequestDto
        {
            CategoryId = categoryId,
            ItemType = string.IsNullOrEmpty(type) ? "Login" : type
        };
        return View(model);
    }

    // POST: /User/Vault/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateVaultItemRequestDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError("Title", "Title is required");
        }

        if (!ModelState.IsValid)
        {
            SetCommonViewBag("Vault");
            ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
            ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
            return View(model);
        }

        try
        {
            await _vaultService.CreateVaultItemAsync(CurrentUserId, model);
            TempData["Success"] = $"{model.ItemType} successfully encrypted and saved in your vault!";
            return RedirectToAction(nameof(Index), new { type = model.ItemType, categoryId = model.CategoryId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["QuotaError"] = ex.Message;
            return RedirectToAction("Index", "Subscription", new { area = "User" });
        }
    }

    // GET: /User/Vault/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        SetCommonViewBag("Vault");
        var item = await _vaultService.GetVaultItemByIdAsync(CurrentUserId, id);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
        return View(item);
    }

    // GET: /User/Vault/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        SetCommonViewBag("Vault");
        var item = await _vaultService.GetVaultItemByIdAsync(CurrentUserId, id);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);

        var model = new UpdateVaultItemRequestDto
        {
            Id = item.Id,
            Title = item.Title,
            CategoryId = item.CategoryId,
            ItemType = item.ItemType,
            Username = item.Username,
            Password = item.Password ?? "",
            Url = item.Url,
            Notes = item.Notes,
            ItemIcon = item.ItemIcon,
            IsFavorite = item.IsFavorite,
            CardholderName = item.CardholderName,
            CardNumber = item.CardNumber,
            CardBrand = item.CardBrand,
            CardExpiryMonth = item.CardExpiryMonth,
            CardExpiryYear = item.CardExpiryYear,
            CardCvv = item.CardCvv,
            CardPin = item.CardPin,
            DocContent = item.DocContent,
            DocFileName = item.DocFileName,
            DocFileSize = item.DocFileSize,
            DocMimeType = item.DocMimeType,
            ApiKey = item.ApiKey,
            ApiSecret = item.ApiSecret,
            ApiEnvironment = item.ApiEnvironment
        };
        return View(model);
    }

    // POST: /User/Vault/Edit
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateVaultItemRequestDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError("Title", "Title is required");
        }

        if (!ModelState.IsValid)
        {
            SetCommonViewBag("Vault");
            ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
            ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
            return View(model);
        }

        var ok = await _vaultService.UpdateVaultItemAsync(CurrentUserId, model);
        if (!ok)
        {
            TempData["Error"] = "Failed to update item.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Vault item updated and re-encrypted successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // POST: /User/Vault/Delete/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var ok = await _vaultService.DeleteVaultItemAsync(CurrentUserId, id);
        TempData[ok ? "Success" : "Error"] = ok ? "Item moved to Trash." : "Item not found.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Vault/ToggleFavorite/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(string id, string? returnUrl = null)
    {
        await _vaultService.ToggleFavoriteAsync(CurrentUserId, id);
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    // ── TRASH MANAGEMENT ───────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Trash()
    {
        SetCommonViewBag("Vault");
        ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        ViewBag.Counts = await _vaultService.GetVaultCountsAsync(CurrentUserId);
        ViewBag.SelectedType = "Trash";

        var trashItems = await _vaultService.GetTrashItemsAsync(CurrentUserId);
        return View(trashItems);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var ok = await _vaultService.RestoreFromTrashAsync(CurrentUserId, id);
        TempData[ok ? "Success" : "Error"] = ok ? "Item restored back to your vault." : "Failed to restore item.";
        return RedirectToAction(nameof(Trash));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PermanentDelete(string id)
    {
        var ok = await _vaultService.PermanentlyDeleteItemAsync(CurrentUserId, id);
        TempData[ok ? "Success" : "Error"] = ok ? "Item permanently erased from vault." : "Failed to delete item.";
        return RedirectToAction(nameof(Trash));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmptyTrash()
    {
        var ok = await _vaultService.EmptyTrashAsync(CurrentUserId);
        TempData[ok ? "Success" : "Error"] = ok ? "Trash emptied successfully." : "Failed to empty trash.";
        return RedirectToAction(nameof(Trash));
    }

    // ── CATEGORIES ─────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CreateCategoryRequestDto model)
    {
        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            await _vaultService.CreateCategoryAsync(CurrentUserId, model);
            TempData["Success"] = $"Category '{model.Name}' created.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ── PASSWORD GENERATOR ─────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult GeneratePassword(int length = 16, bool uppercase = true, bool lowercase = true, bool numbers = true, bool symbols = true)
    {
        var options = new PasswordGeneratorOptionsDto
        {
            Length = length,
            IncludeUppercase = uppercase,
            IncludeLowercase = lowercase,
            IncludeNumbers = numbers,
            IncludeSymbols = symbols,
            AvoidAmbiguous = true
        };
        var result = _passGen.GeneratePassword(options);
        return Json(result);
    }

    private void SetCommonViewBag(string activePage)
    {
        ViewBag.UserName   = CurrentUserName;
        ViewBag.UserEmail  = CurrentUserEmail;
        ViewBag.IsMfa      = IsMfa;
        ViewBag.ActivePage = activePage;
    }
}
