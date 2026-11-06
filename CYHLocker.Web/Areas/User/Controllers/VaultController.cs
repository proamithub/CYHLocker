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

    // GET: /User/Vault?categoryId=...&q=...
    public async Task<IActionResult> Index(string? categoryId, string? q)
    {
        SetCommonViewBag("Vault");
        ViewBag.SelectedCategory = categoryId;
        ViewBag.SearchQuery = q;

        var items = await _vaultService.GetVaultItemsAsync(CurrentUserId, categoryId, q);
        var categories = await _vaultService.GetCategoriesAsync(CurrentUserId);

        ViewBag.Categories = categories;
        return View(items);
    }

    // GET: /User/Vault/Create
    [HttpGet]
    public async Task<IActionResult> Create(string? categoryId)
    {
        SetCommonViewBag("Vault");
        ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
        
        var model = new CreateVaultItemRequestDto
        {
            CategoryId = categoryId,
            ItemType = "Login"
        };
        return View(model);
    }

    // POST: /User/Vault/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateVaultItemRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            SetCommonViewBag("Vault");
            ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
            return View(model);
        }

        await _vaultService.CreateVaultItemAsync(CurrentUserId, model);
        TempData["Success"] = "Item safely encrypted and stored in your vault!";
        return RedirectToAction(nameof(Index));
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
            IsFavorite = item.IsFavorite
        };
        return View(model);
    }

    // POST: /User/Vault/Edit
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateVaultItemRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            SetCommonViewBag("Vault");
            ViewBag.Categories = await _vaultService.GetCategoriesAsync(CurrentUserId);
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
        TempData[ok ? "Success" : "Error"] = ok ? "Item removed from vault." : "Item not found.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Vault/ToggleFavorite/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(string id)
    {
        await _vaultService.ToggleFavoriteAsync(CurrentUserId, id);
        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Vault/CreateCategory
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

    // AJAX: /User/Vault/GeneratePassword
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
