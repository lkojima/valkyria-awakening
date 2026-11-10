using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/gacha")]
    [Authorize]
    public class GachaController : ControllerBase
    {
        private readonly IGachaService _gachaService;

        public GachaController(IGachaService gachaService)
        {
            _gachaService = gachaService;
        }

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners()
        {
            var userId = GetCurrentUserId();
            var banners = await _gachaService.GetAvailableBannersAsync(userId);
            return Ok(banners);
        }

        [HttpGet("banners/{bannerId:guid}")]
        public async Task<IActionResult> GetBannerDetail(Guid bannerId)
        {
            var banner = await _gachaService.GetBannerDetailAsync(bannerId);
            if (banner == null)
                return NotFound();
            return Ok(banner);
        }

        [HttpPost("summon")]
        public async Task<IActionResult> Summon([FromBody] SummonRequest request)
        {
            var userId = GetCurrentUserId();
            var cost = new CurrencyAmount(request.CurrencyType, request.Amount);
            var result = await _gachaService.SummonAsync(userId, request.BannerId, request.IsMulti, cost);
            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 50)
        {
            var userId = GetCurrentUserId();
            var history = await _gachaService.GetSummonHistoryAsync(userId, limit);
            return Ok(history);
        }

        [HttpGet("pity/{bannerId:guid}")]
        public async Task<IActionResult> GetPityStatus(Guid bannerId)
        {
            var userId = GetCurrentUserId();
            var pity = await _gachaService.GetPityStatusAsync(userId, bannerId);
            return Ok(pity);
        }

        [HttpPost("animation")]
        public async Task<IActionResult> GetAnimation([FromBody] AnimationRequest request)
        {
            var animation = await _gachaService.GetSummonAnimationAsync(request.BannerId, request.Results);
            return Ok(animation);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class SummonRequest
    {
        public Guid BannerId { get; set; }
        public bool IsMulti { get; set; }
        public CurrencyType CurrencyType { get; set; }
        public long Amount { get; set; }
    }

    public class AnimationRequest
    {
        public Guid BannerId { get; set; }
        public List<GachaResult> Results { get; set; }
    }
}