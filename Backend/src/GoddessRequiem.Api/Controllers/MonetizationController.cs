using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/monetization")]
    [Authorize]
    public class MonetizationController : ControllerBase
    {
        private readonly IMonetizationService _monetizationService;

        public MonetizationController(IMonetizationService monetizationService)
        {
            _monetizationService = monetizationService;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var userId = GetCurrentUserId();
            var products = await _monetizationService.GetProductsAsync(userId);
            return Ok(products);
        }

        [HttpPost("purchases/validate")]
        public async Task<IActionResult> ValidatePurchase([FromBody] ValidatePurchaseRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _monetizationService.ValidatePurchaseAsync(userId, request.ProductId, request.PurchaseToken, request.ReceiptData);
            return Ok(result);
        }

        [HttpGet("subscription")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            var userId = GetCurrentUserId();
            var status = await _monetizationService.GetSubscriptionStatusAsync(userId);
            return Ok(status);
        }

        [HttpGet("battle-pass")]
        public async Task<IActionResult> GetBattlePassInfo()
        {
            var userId = GetCurrentUserId();
            var info = await _monetizationService.GetBattlePassInfoAsync(userId);
            return Ok(info);
        }

        [HttpPost("battle-pass/{tier:int}/claim")]
        public async Task<IActionResult> ClaimBattlePassReward(int tier, [FromBody] ClaimBattlePassRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _monetizationService.ClaimBattlePassRewardAsync(userId, tier, request.IsPremium);
            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPurchaseHistory([FromQuery] int limit = 50)
        {
            var userId = GetCurrentUserId();
            var history = await _monetizationService.GetPurchaseHistoryAsync(userId, limit);
            return Ok(history);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class ValidatePurchaseRequest
    {
        public string ProductId { get; set; }
        public string PurchaseToken { get; set; }
        public string ReceiptData { get; set; }
    }

    public class ClaimBattlePassRequest
    {
        public bool IsPremium { get; set; }
    }
}