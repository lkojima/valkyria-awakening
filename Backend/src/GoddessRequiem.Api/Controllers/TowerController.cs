using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoddessRequiem.Core.Application.Interfaces;

namespace GoddessRequiem.Api.Controllers
{
    [ApiController]
    [Route("api/v1/tower")]
    [Authorize]
    public class TowerController : ControllerBase
    {
        private readonly ITowerService _towerService;

        public TowerController(ITowerService towerService)
        {
            _towerService = towerService;
        }

        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            var userId = GetCurrentUserId();
            var progress = await _towerService.GetProgressAsync(userId);
            return Ok(progress);
        }

        [HttpGet("floors/{floor:int}")]
        public async Task<IActionResult> GetFloorDetail(int floor)
        {
            var userId = GetCurrentUserId();
            var floorDetail = await _towerService.GetFloorDetailAsync(userId, floor);
            if (floorDetail == null)
                return NotFound();
            return Ok(floorDetail);
        }

        [HttpPost("floors/{floor:int}/clear")]
        public async Task<IActionResult> ClearFloor(int floor, [FromBody] BattleResultData result)
        {
            var userId = GetCurrentUserId();
            var clearResult = await _towerService.ClearFloorAsync(userId, floor, result);
            return Ok(clearResult);
        }

        [HttpPost("floors/{floor:int}/reward")]
        public async Task<IActionResult> ClaimFloorReward(int floor)
        {
            var userId = GetCurrentUserId();
            var result = await _towerService.ClaimFloorRewardAsync(userId, floor);
            return Ok(result);
        }

        [HttpPost("stars/{stars:int}/reward")]
        public async Task<IActionResult> ClaimStarReward(int stars)
        {
            var userId = GetCurrentUserId();
            var result = await _towerService.ClaimStarRewardAsync(userId, stars);
            return Ok(result);
        }

        [HttpPost("season/reset")]
        public async Task<IActionResult> ResetSeason()
        {
            var userId = GetCurrentUserId();
            var result = await _towerService.ResetSeasonAsync(userId);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }
}