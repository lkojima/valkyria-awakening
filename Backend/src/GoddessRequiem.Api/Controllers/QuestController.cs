using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/quests")]
    [Authorize]
    public class QuestController : ControllerBase
    {
        private readonly IQuestService _questService;

        public QuestController(IQuestService questService)
        {
            _questService = questService;
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveQuests()
        {
            var userId = GetCurrentUserId();
            var quests = await _questService.GetActiveQuestsAsync(userId);
            return Ok(quests);
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableQuests()
        {
            var userId = GetCurrentUserId();
            var quests = await _questService.GetAvailableQuestsAsync(userId);
            return Ok(quests);
        }

        [HttpPost("{questId:guid}/accept")]
        public async Task<IActionResult> AcceptQuest(Guid questId)
        {
            var userId = GetCurrentUserId();
            var result = await _questService.AcceptQuestAsync(userId, questId);
            return Ok(result);
        }

        [HttpPost("{questId:guid}/progress")]
        public async Task<IActionResult> UpdateProgress(Guid questId, [FromBody] UpdateQuestProgressRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _questService.UpdateProgressAsync(userId, questId, request.ObjectiveId, request.Amount);
            return Ok(result);
        }

        [HttpPost("{questId:guid}/claim")]
        public async Task<IActionResult> ClaimReward(Guid questId)
        {
            var userId = GetCurrentUserId();
            var result = await _questService.ClaimQuestRewardAsync(userId, questId);
            return Ok(result);
        }

        [HttpPost("daily/refresh")]
        public async Task<IActionResult> RefreshDailyQuests()
        {
            var userId = GetCurrentUserId();
            var result = await _questService.RefreshDailyQuestsAsync(userId);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class UpdateQuestProgressRequest
    {
        public string ObjectiveId { get; set; }
        public int Amount { get; set; } = 1;
    }
}