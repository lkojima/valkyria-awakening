using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/runes")]
    [Authorize]
    public class RuneController : ControllerBase
    {
        private readonly IRuneService _runeService;

        public RuneController(IRuneService runeService)
        {
            _runeService = runeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserRunes()
        {
            var userId = GetCurrentUserId();
            var runes = await _runeService.GetUserRunesAsync(userId);
            return Ok(runes);
        }

        [HttpGet("{runeId:guid}")]
        public async Task<IActionResult> GetRune(Guid runeId)
        {
            var rune = await _runeService.GetRuneAsync(runeId);
            if (rune == null)
                return NotFound();
            return Ok(rune);
        }

        [HttpPost("{runeId:guid}/enhance")]
        public async Task<IActionResult> Enhance(Guid runeId)
        {
            var userId = GetCurrentUserId();
            var result = await _runeService.EnhanceRuneAsync(userId, runeId);
            return Ok(result);
        }

        [HttpPost("equip")]
        public async Task<IActionResult> Equip([FromBody] EquipRuneRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _runeService.EquipAsync(userId, request.CharacterId, request.RuneId, request.Slot);
            return Ok(result);
        }

        [HttpPost("unequip")]
        public async Task<IActionResult> Unequip([FromBody] UnequipRuneRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _runeService.UnequipAsync(userId, request.CharacterId, request.Slot);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class EquipRuneRequest
    {
        public Guid CharacterId { get; set; }
        public Guid RuneId { get; set; }
        public int Slot { get; set; }
    }

    public class UnequipRuneRequest
    {
        public Guid CharacterId { get; set; }
        public int Slot { get; set; }
    }
}