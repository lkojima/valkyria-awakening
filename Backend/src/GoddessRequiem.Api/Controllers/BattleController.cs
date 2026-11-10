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
    [Route("api/v1/battle")]
    [Authorize]
    public class BattleController : ControllerBase
    {
        private readonly IBattleService _battleService;
        private readonly IAutoBattleService _autoBattleService;

        public BattleController(IBattleService battleService, IAutoBattleService autoBattleService)
        {
            _battleService = battleService;
            _autoBattleService = autoBattleService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartBattle([FromBody] StartBattleRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _battleService.StartBattleAsync(
                userId,
                request.Mode,
                request.StageId,
                request.CharacterIds,
                request.Speed,
                request.AutoMode);
            return Ok(result);
        }

        [HttpPost("{battleId:guid}/action")]
        public async Task<IActionResult> ProcessAction(Guid battleId, [FromBody] BattleActionRequest request)
        {
            var result = await _battleService.ProcessActionAsync(battleId, request.Action);
            return Ok(result);
        }

        [HttpPost("{battleId:guid}/auto-turn")]
        public async Task<IActionResult> AutoBattleTurn(Guid battleId)
        {
            var result = await _battleService.AutoBattleTurnAsync(battleId);
            return Ok(result);
        }

        [HttpPost("{battleId:guid}/end")]
        public async Task<IActionResult> EndBattle(Guid battleId, [FromBody] EndBattleRequest request)
        {
            var result = await _battleService.EndBattleAsync(battleId, request.Surrender);
            return Ok(result);
        }

        [HttpGet("{battleId:guid}/state")]
        public async Task<IActionResult> GetBattleState(Guid battleId)
        {
            var state = await _battleService.GetBattleStateAsync(battleId);
            return Ok(state);
        }

        [HttpGet("replays")]
        public async Task<IActionResult> GetReplays([FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            var replays = await _battleService.GetBattleReplaysAsync(userId, limit);
            return Ok(replays);
        }

        [HttpGet("auto/config")]
        public async Task<IActionResult> GetAutoBattleConfig()
        {
            var userId = GetCurrentUserId();
            var config = await _autoBattleService.GetConfigAsync(userId);
            return Ok(config);
        }

        [HttpPut("auto/config")]
        public async Task<IActionResult> UpdateAutoBattleConfig([FromBody] AutoBattleConfig config)
        {
            var userId = GetCurrentUserId();
            await _autoBattleService.UpdateConfigAsync(userId, config);
            return Ok(new { success = true });
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class StartBattleRequest
    {
        public GameMode Mode { get; set; }
        public int StageId { get; set; }
        public List<Guid> CharacterIds { get; set; } = new();
        public BattleSpeed Speed { get; set; } = BattleSpeed.Normal;
        public AutoBattleMode AutoMode { get; set; } = AutoBattleMode.Off;
    }

    public class BattleActionRequest
    {
        public BattleAction Action { get; set; }
    }

    public class EndBattleRequest
    {
        public bool Surrender { get; set; } = false;
    }
}