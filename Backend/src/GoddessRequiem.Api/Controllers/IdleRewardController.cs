using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.ValueObjects;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/idle")]
    [Authorize]
    public class IdleRewardController : ControllerBase
    {
        private readonly IIdleRewardService _idleRewardService;

        public IdleRewardController(IIdleRewardService idleRewardService)
        {
            _idleRewardService = idleRewardService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var userId = GetCurrentUserId();
            var status = await _idleRewardService.GetStatusAsync(userId);
            return Ok(status);
        }

        [HttpPost("claim")]
        public async Task<IActionResult> ClaimRewards()
        {
            var userId = GetCurrentUserId();
            var summary = await _idleRewardService.ClaimRewardsAsync(userId);
            return Ok(summary);
        }

        [HttpPut("auto-claim")]
        public async Task<IActionResult> SetAutoClaim([FromBody] AutoClaimRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _idleRewardService.SetAutoClaimAsync(userId, request.Enabled);
            return Ok(new { success });
        }

        [HttpPut("max-accumulation")]
        public async Task<IActionResult> SetMaxAccumulation([FromBody] MaxAccumulationRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _idleRewardService.SetMaxAccumulationAsync(userId, request.Hours);
            return Ok(new { success });
        }

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _idleRewardService.GetConfigAsync();
            return Ok(config);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class AutoClaimRequest
    {
        public bool Enabled { get; set; }
    }

    public class MaxAccumulationRequest
    {
        public int Hours { get; set; }
    }
}