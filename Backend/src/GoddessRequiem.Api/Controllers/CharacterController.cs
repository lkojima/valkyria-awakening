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
    [Route("api/v1/characters")]
    [Authorize]
    public class CharacterController : ControllerBase
    {
        private readonly ICharacterService _characterService;

        public CharacterController(ICharacterService characterService)
        {
            _characterService = characterService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCharacters([FromQuery] Rarity? rarity, [FromQuery] Element? element)
        {
            var characters = await _characterService.GetAllCharactersAsync(rarity, element);
            return Ok(characters);
        }

        [HttpGet("{characterId:guid}")]
        public async Task<IActionResult> GetCharacter(Guid characterId)
        {
            var character = await _characterService.GetCharacterAsync(characterId);
            if (character == null)
                return NotFound();
            return Ok(character);
        }

        [HttpGet("user")]
        public async Task<IActionResult> GetUserCharacters()
        {
            var userId = GetCurrentUserId();
            var characters = await _characterService.GetUserCharactersAsync(userId);
            return Ok(characters);
        }

        [HttpGet("user/{characterId:guid}")]
        public async Task<IActionResult> GetUserCharacter(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var character = await _characterService.GetUserCharacterAsync(userId, characterId);
            if (character == null)
                return NotFound();
            return Ok(character);
        }

        [HttpPost("{characterId:guid}/level-up")]
        public async Task<IActionResult> LevelUp(Guid characterId, [FromBody] LevelUpRequest request)
        {
            var userId = GetCurrentUserId();
            var cost = new CurrencyAmount(request.CurrencyType, request.Amount);
            var result = await _characterService.LevelUpCharacterAsync(userId, characterId, request.TargetLevel, cost);
            return Ok(result);
        }

        [HttpPost("{characterId:guid}/ascend")]
        public async Task<IActionResult> Ascend(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var result = await _characterService.AscendCharacterAsync(userId, characterId);
            return Ok(result);
        }

        [HttpPost("{characterId:guid}/skill/upgrade")]
        public async Task<IActionResult> UpgradeSkill(Guid characterId, [FromBody] SkillUpgradeRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _characterService.UpgradeSkillAsync(userId, characterId, request.SkillType);
            return Ok(result);
        }

        [HttpPost("{characterId:guid}/limit-break")]
        public async Task<IActionResult> LimitBreak(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var result = await _characterService.LimitBreakAsync(userId, characterId);
            return Ok(result);
        }

        [HttpPost("{characterId:guid}/favorite")]
        public async Task<IActionResult> ToggleFavorite(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var success = await _characterService.ToggleFavoriteAsync(userId, characterId);
            return Ok(new { success });
        }

        [HttpPost("{characterId:guid}/lock")]
        public async Task<IActionResult> ToggleLock(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var success = await _characterService.ToggleLockAsync(userId, characterId);
            return Ok(new { success });
        }

        [HttpGet("{characterId:guid}/stats")]
        public async Task<IActionResult> CalculateStats(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var stats = await _characterService.CalculateStatsAsync(userId, characterId);
            return Ok(stats);
        }

        [HttpPost("{characterId:guid}/affinity")]
        public async Task<IActionResult> AddAffinityExp(Guid characterId, [FromBody] AffinityExpRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _characterService.AddAffinityExpAsync(userId, characterId, request.Amount);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class LevelUpRequest
    {
        public int TargetLevel { get; set; }
        public CurrencyType CurrencyType { get; set; }
        public long Amount { get; set; }
    }

    public class SkillUpgradeRequest
    {
        public SkillType SkillType { get; set; }
    }

    public class AffinityExpRequest
    {
        public long Amount { get; set; }
    }
}