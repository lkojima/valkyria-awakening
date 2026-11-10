using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/arena")]
    [Authorize]
    public class ArenaController : ControllerBase
    {
        private readonly IArenaService _arenaService;

        public ArenaController(IArenaService arenaService)
        {
            _arenaService = arenaService;
        }

        [HttpGet("info")]
        public async Task<IActionResult> GetArenaInfo()
        {
            var userId = GetCurrentUserId();
            var info = await _arenaService.GetArenaInfoAsync(userId);
            return Ok(info);
        }

        [HttpGet("opponents")]
        public async Task<IActionResult> GetOpponents()
        {
            var userId = GetCurrentUserId();
            var opponents = await _arenaService.GetOpponentsAsync(userId);
            return Ok(opponents);
        }

        [HttpPost("battle/start")]
        public async Task<IActionResult> StartBattle([FromBody] StartArenaBattleRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.StartBattleAsync(userId, request.OpponentId, request.IsRevenge);
            return Ok(result);
        }

        [HttpPost("battle/result")]
        public async Task<IActionResult> ProcessBattleResult([FromBody] BattleResultData result)
        {
            var userId = GetCurrentUserId();
            var arenaResult = await _arenaService.ProcessBattleResultAsync(userId, result);
            return Ok(arenaResult);
        }

        [HttpPost("rewards/daily")]
        public async Task<IActionResult> ClaimDailyReward()
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.ClaimDailyRewardAsync(userId);
            return Ok(result);
        }

        [HttpPost("rewards/weekly")]
        public async Task<IActionResult> ClaimWeeklyReward()
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.ClaimWeeklyRewardAsync(userId);
            return Ok(result);
        }

        [HttpPost("rewards/season")]
        public async Task<IActionResult> ClaimSeasonReward()
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.ClaimSeasonRewardAsync(userId);
            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshOpponents()
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.RefreshOpponentsAsync(userId);
            return Ok(result);
        }

        [HttpPut("defense-team")]
        public async Task<IActionResult> SetDefenseTeam([FromBody] SetDefenseTeamRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _arenaService.SetDefenseTeamAsync(userId, request.CharacterIds);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class StartArenaBattleRequest
    {
        public Guid OpponentId { get; set; }
        public bool IsRevenge { get; set; } = false;
    }

    public class SetDefenseTeamRequest
    {
        public List<Guid> CharacterIds { get; set; } = new();
    }
}