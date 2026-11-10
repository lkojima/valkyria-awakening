using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.ValueObjects;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/skins")]
    [Authorize]
    public class SkinController : ControllerBase
    {
        private readonly ISkinService _skinService;

        public SkinController(ISkinService skinService)
        {
            _skinService = skinService;
        }

        [HttpGet("character/{characterId:guid}")]
        public async Task<IActionResult> GetAvailableSkins(Guid characterId)
        {
            var skins = await _skinService.GetAvailableSkinsAsync(characterId);
            return Ok(skins);
        }

        [HttpGet("user/character/{characterId:guid}")]
        public async Task<IActionResult> GetUserSkins(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var skins = await _skinService.GetUserSkinsAsync(userId, characterId);
            return Ok(skins);
        }

        [HttpGet("user/{skinId:guid}")]
        public async Task<IActionResult> GetUserSkin(Guid skinId)
        {
            var userId = GetCurrentUserId();
            var skin = await _skinService.GetUserSkinAsync(userId, skinId);
            if (skin == null)
                return NotFound();
            return Ok(skin);
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> PurchaseSkin([FromBody] PurchaseSkinRequest request)
        {
            var userId = GetCurrentUserId();
            var price = new CurrencyAmount(request.CurrencyType, request.Amount);
            var result = await _skinService.PurchaseSkinAsync(userId, request.SkinId, price);
            return Ok(result);
        }

        [HttpPost("equip")]
        public async Task<IActionResult> EquipSkin([FromBody] EquipSkinRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _skinService.EquipSkinAsync(userId, request.CharacterId, request.SkinId);
            return Ok(result);
        }

        [HttpPost("dye")]
        public async Task<IActionResult> DyeSkin([FromBody] DyeSkinRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _skinService.DyeSkinAsync(userId, request.SkinId, request.DyeOption);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class PurchaseSkinRequest
    {
        public Guid SkinId { get; set; }
        public CurrencyType CurrencyType { get; set; }
        public long Amount { get; set; }
    }

    public class EquipSkinRequest
    {
        public Guid CharacterId { get; set; }
        public Guid? SkinId { get; set; }
    }

    public class DyeSkinRequest
    {
        public Guid SkinId { get; set; }
        public SkinDyeOption DyeOption { get; set; }
    }
}