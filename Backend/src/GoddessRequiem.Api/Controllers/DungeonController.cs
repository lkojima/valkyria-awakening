using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/dungeons")]
    [Authorize]
    public class DungeonController : ControllerBase
    {
        private readonly IDungeonService _dungeonService;

        public DungeonController(IDungeonService dungeonService)
        {
            _dungeonService = dungeonService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableDungeons()
        {
            var userId = GetCurrentUserId();
            var dungeons = await _dungeonService.GetAvailableDungeonsAsync(userId);
            return Ok(dungeons);
        }

        [HttpGet("{dungeonId:guid}")]
        public async Task<IActionResult> GetDungeonDetail(Guid dungeonId)
        {
            var userId = GetCurrentUserId();
            var detail = await _dungeonService.GetDungeonDetailAsync(userId, dungeonId);
            if (detail == null)
                return NotFound();
            return Ok(detail);
        }

        [HttpPost("{dungeonId:guid}/clear")]
        public async Task<IActionResult> ClearDungeon(Guid dungeonId, [FromBody] ClearDungeonRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _dungeonService.ClearDungeonAsync(userId, dungeonId, request.Tier, request.BattleResult);
            return Ok(result);
        }

        [HttpPost("{dungeonId:guid}/rewards/stars/{tier:int}")]
        public async Task<IActionResult> ClaimStarRewards(Guid dungeonId, int tier)
        {
            var userId = GetCurrentUserId();
            var result = await _dungeonService.ClaimStarRewardsAsync(userId, dungeonId, tier);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class ClearDungeonRequest
    {
        public int Tier { get; set; }
        public BattleResultData BattleResult { get; set; }
    }
}