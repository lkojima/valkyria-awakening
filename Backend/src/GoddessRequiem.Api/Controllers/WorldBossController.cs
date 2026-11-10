using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/world-boss")]
    [Authorize]
    public class WorldBossController : ControllerBase
    {
        private readonly IWorldBossService _worldBossService;

        public WorldBossController(IWorldBossService worldBossService)
        {
            _worldBossService = worldBossService;
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveBoss()
        {
            var boss = await _worldBossService.GetActiveBossAsync();
            if (boss == null)
                return NotFound();
            return Ok(boss);
        }

        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            var userId = GetCurrentUserId();
            var progress = await _worldBossService.GetProgressAsync(userId);
            return Ok(progress);
        }

        [HttpPost("battle/start")]
        public async Task<IActionResult> StartBattle([FromBody] StartWorldBossBattleRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _worldBossService.StartBattleAsync(userId, request.UseFreeEntry);
            return Ok(result);
        }

        [HttpPost("battle/submit")]
        public async Task<IActionResult> SubmitDamage([FromBody] SubmitWorldBossDamageRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _worldBossService.SubmitDamageAsync(userId, request.Damage, request.BattleResult);
            return Ok(result);
        }

        [HttpPost("rewards/damage/{thresholdIndex:int}")]
        public async Task<IActionResult> ClaimDamageReward(int thresholdIndex)
        {
            var userId = GetCurrentUserId();
            var result = await _worldBossService.ClaimDamageRewardAsync(userId, thresholdIndex);
            return Ok(result);
        }

        [HttpPost("rewards/participation")]
        public async Task<IActionResult> ClaimParticipationReward()
        {
            var userId = GetCurrentUserId();
            var result = await _worldBossService.ClaimParticipationRewardAsync(userId);
            return Ok(result);
        }

        [HttpPost("rewards/ranking")]
        public async Task<IActionResult> ClaimRankingReward()
        {
            var userId = GetCurrentUserId();
            var result = await _worldBossService.ClaimRankingRewardAsync(userId);
            return Ok(result);
        }

        [HttpGet("ranking")]
        public async Task<IActionResult> GetRanking([FromQuery] int count = 100)
        {
            var ranking = await _worldBossService.GetRankingAsync(count);
            return Ok(ranking);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class StartWorldBossBattleRequest
    {
        public bool UseFreeEntry { get; set; } = true;
    }

    public class SubmitWorldBossDamageRequest
    {
        public long Damage { get; set; }
        public BattleResultData BattleResult { get; set; }
    }
}