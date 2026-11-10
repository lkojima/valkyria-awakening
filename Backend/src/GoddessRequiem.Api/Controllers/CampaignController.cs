using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/campaign")]
    [Authorize]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;

        public CampaignController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            var userId = GetCurrentUserId();
            var progress = await _campaignService.GetProgressAsync(userId);
            return Ok(progress);
        }

        [HttpGet("stages/{stageId:int}")]
        public async Task<IActionResult> GetStageDetail(int stageId)
        {
            var userId = GetCurrentUserId();
            var stage = await _campaignService.GetStageDetailAsync(userId, stageId);
            if (stage == null)
                return NotFound();
            return Ok(stage);
        }

        [HttpPost("stages/{stageId:int}/clear")]
        public async Task<IActionResult> ClearStage(int stageId, [FromBody] BattleResultData result)
        {
            var userId = GetCurrentUserId();
            var clearResult = await _campaignService.ClearStageAsync(userId, stageId, result);
            return Ok(clearResult);
        }

        [HttpPost("chapters/{chapter:int}/reward")]
        public async Task<IActionResult> ClaimChapterReward(int chapter)
        {
            var userId = GetCurrentUserId();
            var result = await _campaignService.ClaimChapterRewardAsync(userId, chapter);
            return Ok(result);
        }

        [HttpPost("auto-progress/start")]
        public async Task<IActionResult> StartAutoProgress([FromBody] AutoProgressConfig config)
        {
            var userId = GetCurrentUserId();
            var result = await _campaignService.StartAutoProgressAsync(userId, config);
            return Ok(result);
        }

        [HttpPost("auto-progress/stop")]
        public async Task<IActionResult> StopAutoProgress()
        {
            var userId = GetCurrentUserId();
            await _campaignService.StopAutoProgressAsync(userId);
            return Ok(new { success = true });
        }

        [HttpGet("auto-progress/status")]
        public async Task<IActionResult> GetAutoProgressStatus()
        {
            var userId = GetCurrentUserId();
            var status = await _campaignService.GetAutoProgressStatusAsync(userId);
            return Ok(status);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }
}