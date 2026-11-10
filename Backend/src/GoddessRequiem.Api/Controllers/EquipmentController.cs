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
    [Route("api/v1/equipment")]
    [Authorize]
    public class EquipmentController : ControllerBase
    {
        private readonly IEquipmentService _equipmentService;

        public EquipmentController(IEquipmentService equipmentService)
        {
            _equipmentService = equipmentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserEquipment()
        {
            var userId = GetCurrentUserId();
            var equipment = await _equipmentService.GetUserEquipmentAsync(userId);
            return Ok(equipment);
        }

        [HttpGet("{equipmentId:guid}")]
        public async Task<IActionResult> GetEquipment(Guid equipmentId)
        {
            var equipment = await _equipmentService.GetEquipmentAsync(equipmentId);
            if (equipment == null)
                return NotFound();
            return Ok(equipment);
        }

        [HttpPost("{equipmentId:guid}/enhance")]
        public async Task<IActionResult> Enhance(Guid equipmentId)
        {
            var userId = GetCurrentUserId();
            var result = await _equipmentService.EnhanceEquipmentAsync(userId, equipmentId);
            return Ok(result);
        }

        [HttpPost("equip")]
        public async Task<IActionResult> Equip([FromBody] EquipRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _equipmentService.EquipAsync(userId, request.CharacterId, request.EquipmentId, request.Slot);
            return Ok(result);
        }

        [HttpPost("unequip")]
        public async Task<IActionResult> Unequip([FromBody] UnequipRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _equipmentService.UnequipAsync(userId, request.CharacterId, request.Slot);
            return Ok(result);
        }

        [HttpPost("sell")]
        public async Task<IActionResult> Sell([FromBody] SellRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _equipmentService.SellEquipmentAsync(userId, request.EquipmentIds);
            return Ok(result);
        }

        [HttpPost("auto-equip/{characterId:guid}")]
        public async Task<IActionResult> AutoEquip(Guid characterId)
        {
            var userId = GetCurrentUserId();
            var result = await _equipmentService.AutoEquipAsync(userId, characterId);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    public class EquipRequest
    {
        public Guid CharacterId { get; set; }
        public Guid EquipmentId { get; set; }
        public EquipmentSlot Slot { get; set; }
    }

    public class UnequipRequest
    {
        public Guid CharacterId { get; set; }
        public EquipmentSlot Slot { get; set; }
    }

    public class SellRequest
    {
        public List<Guid> EquipmentIds { get; set; } = new();
    }
}