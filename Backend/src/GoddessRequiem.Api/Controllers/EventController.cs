using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorize;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/events")]
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventController(IEventService eventService)
        {
            _eventService = eventService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveEvents()
        {
            var userId = GetCurrentUserId();
            var events = await _eventService.GetActiveEventsAsync(userId);
            return Ok(events);
        }

        [HttpGet("{eventId:guid}/progress")]
        public async Task<IActionResult> GetEventProgress(Guid eventId)
        {
            var userId = GetCurrentUserId();
            var progress = await _eventService.GetEventProgressAsync(userId, eventId);
            return Ok(progress);
        }

        [HttpPost("{eventId:guid}/stages/{stageId:guid}/clear")]
        public async Task<IActionResult> ClearEventStage(Guid eventId, Guid stageId, [FromBody] BattleResultData result)
        {
            var userId = GetCurrentUserId();
            var clearResult = await _eventService.ClearEventStageAsync(userId, eventId, stageId, result);
            return Ok(clearResult);
        }

        [HttpPost("{eventId:guid}/quests/{questId:guid}/progress")]
        public async Task<IActionResult> UpdateEventQuestProgress(Guid eventId, Guid questId, [FromBody] Dictionary<string, int> progress)
        {
            var userId = GetCurrentUserId();
            var result = await _eventService.UpdateEventQuestProgressAsync(userId, eventId, questId, progress);
            return Ok(result);
        }

        [HttpPost("{eventId:guid}/quests/{questId:guid}/claim")]
        public async Task<IActionResult> ClaimEventQuestReward(Guid eventId, Guid questId)
        {
            var userId = GetCurrentUserId();
            var result = await _eventService.ClaimEventQuestRewardAsync(userId, eventId, questId);
            return Ok(result);
        }

        [HttpPost("{eventId:guid}/milestones/{pointsThreshold:int}/claim")]
        public async Task<IActionResult> ClaimMilestoneReward(Guid eventId, int pointsThreshold)
        {
            var userId = GetCurrentUserId();
            var result = await _eventService.ClaimMilestoneRewardAsync(userId, eventId, pointsThreshold);
            return Ok(result);
        }

        [HttpPost("{eventId:guid}/shops/{shopId:guid}/purchase")]
        public async Task<IActionResult> PurchaseFromEventShop(Guid eventId, Guid shopId, [FromBody] PurchaseEventShopRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _eventService.PurchaseFromEventShopAsync(userId, eventId, shopId, request.ItemId, request.Quantity);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class PurchaseEventShopRequest
    {
        public Guid ItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}