using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.User.Controllers;

[Area("User")]
[Authorize]
public class SubscriptionController : Controller
{
    private readonly ISubscriptionService _subService;

    public SubscriptionController(ISubscriptionService subService)
    {
        _subService = subService;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string CurrentUserName => User.Identity?.Name ?? "User";
    private string CurrentUserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool IsMfa => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");

    // GET: /User/Subscription/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        SetCommonViewBag("Subscription");

        var userSub = await _subService.GetUserSubscriptionAsync(CurrentUserId);
        var allPlans = await _subService.GetPlansAsync();

        ViewBag.UserSub = userSub;
        ViewBag.Plans = allPlans;

        return View();
    }

    // POST: /User/Subscription/CreateOrder (AJAX endpoint)
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PlanCode))
            return BadRequest(new { success = false, message = "Plan code is required." });

        try
        {
            var order = await _subService.CreateRazorpayOrderAsync(
                CurrentUserId, 
                CurrentUserEmail, 
                CurrentUserName, 
                req.PlanCode);

            return Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // POST: /User/Subscription/VerifyPayment (AJAX or Form Post)
    [HttpPost]
    public async Task<IActionResult> VerifyPayment([FromBody] RazorpayPaymentVerifyDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid payment payload." });

        var (ok, msg) = await _subService.VerifyAndActivateSubscriptionAsync(
            CurrentUserId, 
            CurrentUserEmail, 
            CurrentUserName, 
            model);

        return Json(new { success = ok, message = msg });
    }

    private void SetCommonViewBag(string activePage)
    {
        ViewBag.UserName   = CurrentUserName;
        ViewBag.UserEmail  = CurrentUserEmail;
        ViewBag.IsMfa      = IsMfa;
        ViewBag.ActivePage = activePage;
    }

    public class CreateOrderRequest
    {
        public string PlanCode { get; set; } = string.Empty;
    }
}
