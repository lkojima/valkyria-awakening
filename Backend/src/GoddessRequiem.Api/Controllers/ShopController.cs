using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/shops")]
    [Authorize]
    public class ShopController : ControllerBase
    {
        private readonly IShopService _shopService;

        public ShopController(IShopService shopService)
        {
            _shopService = shopService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableShops()
        {
            var userId = GetCurrentUserId();
            var shops = await _shopService.GetAvailableShopsAsync(userId);
            return Ok(shops);
        }

        [HttpGet("{shopId:guid}")]
        public async Task<IActionResult> GetShopDetail(Guid shopId)
        {
            var userId = GetCurrentUserId();
            var detail = await _shopService.GetShopDetailAsync(userId, shopId);
            if (detail == null)
                return NotFound();
            return Ok(detail);
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase([FromBody] PurchaseRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _shopService.PurchaseAsync(userId, request.ShopId, request.ItemId, request.Quantity);
            return Ok(result);
        }

        [HttpPost("{shopId:guid}/refresh")]
        public async Task<IActionResult> RefreshShop(Guid shopId)
        {
            var userId = GetCurrentUserId();
            var result = await _shopService.RefreshShopAsync(userId, shopId);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class PurchaseRequest
    {
        public Guid ShopId { get; set; }
        public Guid ItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}