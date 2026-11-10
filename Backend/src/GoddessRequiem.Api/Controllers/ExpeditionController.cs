using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/expeditions")]
    [Authorize]
    public class ExpeditionController : ControllerBase
    {
        private readonly IExpeditionService _expeditionService;

        public ExpeditionController(IExpeditionService expeditionService)
        {
            _expeditionService = expeditionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableExpeditions()
        {
            var userId = GetCurrentUserId();
            var expeditions = await _expeditionService.GetAvailableExpeditionsAsync(userId);
            return Ok(expeditions);
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartExpedition([FromBody] StartExpeditionRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _expeditionService.StartExpeditionAsync(
                userId,
                request.ExpeditionId,
                request.CharacterIds,
                request.AutoRepeat,
                request.MaxRepeats);
            return Ok(result);
        }

        [HttpPost("{userExpeditionId:guid}/complete")]
        public async Task<IActionResult> CompleteExpedition(Guid userExpeditionId)
        {
            var userId = GetCurrentUserId();
            var result = await _expeditionService.CompleteExpeditionAsync(userId, userExpeditionId);
            return Ok(result);
        }

        [HttpPost("{userExpeditionId:guid}/claim")]
        public async Task<IActionResult> ClaimRewards(Guid userExpeditionId)
        {
            var userId = GetCurrentUserId();
            var result = await _expeditionService.ClaimExpeditionRewardsAsync(userId, userExpeditionId);
            return Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveExpeditions()
        {
            var userId = GetCurrentUserId();
            var expeditions = await _expeditionService.GetActiveExpeditionsAsync(userId);
            return Ok(expeditions);
        }

        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedExpeditions()
        {
            var userId = GetCurrentUserId();
            var expeditions = await _expeditionService.GetCompletedExpeditionsAsync(userId);
            return Ok(expeditions);
        }

        [HttpDelete("{userExpeditionId:guid}")]
        public async Task<IActionResult> CancelExpedition(Guid userExpeditionId)
        {
            var userId = GetCurrentUserId();
            var success = await _expeditionService.CancelExpeditionAsync(userId, userExpeditionId);
            return Ok(new { success });
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class StartExpeditionRequest
    {
        public Guid ExpeditionId { get; set; }
        public List<Guid> CharacterIds { get; set; } = new();
        public bool AutoRepeat { get; set; } = false;
        public int MaxRepeats { get; set; } = 0;
    }
}