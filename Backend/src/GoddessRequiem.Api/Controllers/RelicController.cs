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
    [Route("api/v1/relics")]
    [Authorize]
    public class RelicController : ControllerBase
    {
        private readonly IRelicService _relicService;

        public RelicController(IRelicService relicService)
        {
            _relicService = relicService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserRelics()
        {
            var userId = GetCurrentUserId();
            var relics = await _relicService.GetUserRelicsAsync(userId);
            return Ok(relics);
        }

        [HttpGet("{relicId:guid}")]
        public async Task<IActionResult> GetRelic(Guid relicId)
        {
            var relic = await _relicService.GetRelicAsync(relicId);
            if (relic == null)
                return NotFound();
            return Ok(relic);
        }

        [HttpPost("{relicId:guid}/enhance")]
        public async Task<IActionResult> Enhance(Guid relicId)
        {
            var userId = GetCurrentUserId();
            var result = await _relicService.EnhanceRelicAsync(userId, relicId);
            return Ok(result);
        }

        [HttpPost("equip")]
        public async Task<IActionResult> Equip([FromBody] EquipRelicRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _relicService.EquipAsync(userId, request.CharacterId, request.RelicId);
            return Ok(result);
        }

        [HttpPost("unequip")]
        public async Task<IActionResult> Unequip([FromBody] UnequipRelicRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _relicService.UnequipAsync(userId, request.CharacterId);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class EquipRelicRequest
    {
        public Guid CharacterId { get; set; }
        public Guid RelicId { get; set; }
    }

    public class UnequipRelicRequest
    {
        public Guid CharacterId { get; set; }
    }
}