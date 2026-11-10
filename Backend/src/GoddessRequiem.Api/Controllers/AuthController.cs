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
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;

        public AuthController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password,
                request.Platform,
                request.DeviceId,
                request.Language);

            if (!result.Success)
                return BadRequest(new { error = result.Error });

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.Username, request.Password, request.DeviceId);

            if (!result.Success)
                return Unauthorized(new { error = result.Error });

            return Ok(result);
        }

        [HttpPost("login/device")]
        public async Task<IActionResult> LoginWithDevice([FromBody] DeviceLoginRequest request)
        {
            var result = await _authService.LoginWithDeviceIdAsync(request.DeviceId);

            if (!result.Success)
                return Unauthorized(new { error = result.Error });

            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);

            if (!result.Success)
                return Unauthorized(new { error = result.Error });

            return Ok(result);
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var success = await _authService.VerifyEmailAsync(request.UserId, request.Token);
            return Ok(new { success });
        }

        [HttpPost("password/reset/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request)
        {
            var success = await _authService.RequestPasswordResetAsync(request.Email);
            return Ok(new { success });
        }

        [HttpPost("password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var success = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            return Ok(new { success });
        }

        [Authorize]
        [HttpPost("password/change")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new { success });
        }

        [Authorize]
        [HttpPost("revoke-tokens")]
        public async Task<IActionResult> RevokeAllTokens()
        {
            var userId = GetCurrentUserId();
            await _authService.RevokeAllTokensAsync(userId);
            return Ok(new { success = true });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            var profile = await _userService.GetProfileAsync(userId);
            return Ok(profile);
        }

        [Authorize]
        [HttpPut("profile/display-name")]
        public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.UpdateDisplayNameAsync(userId, request.DisplayName);
            return Ok(new { success });
        }

        [Authorize]
        [HttpPut("profile/language")]
        public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.UpdateLanguageAsync(userId, request.Language);
            return Ok(new { success });
        }

        [Authorize]
        [HttpPut("profile/settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.UpdateSettingsAsync(userId, request.Settings);
            return Ok(new { success });
        }

        [Authorize]
        [HttpPost("daily-login/claim")]
        public async Task<IActionResult> ClaimDailyLogin()
        {
            var userId = GetCurrentUserId();
            var result = await _userService.ClaimDailyLoginAsync(userId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("vip")]
        public async Task<IActionResult> GetVipInfo()
        {
            var userId = GetCurrentUserId();
            var info = await _userService.GetVipInfoAsync(userId);
            return Ok(info);
        }

        [Authorize]
        [HttpDelete("account")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.DeleteAccountAsync(userId, request.Password);
            return Ok(new { success });
        }

        [Authorize]
        [HttpGet("account/deletion-status")]
        public async Task<IActionResult> GetDeletionStatus()
        {
            var userId = GetCurrentUserId();
            var status = await _userService.GetDeletionStatusAsync(userId);
            return Ok(status);
        }

        // LGPD Compliance Endpoints
        [Authorize]
        [HttpPost("consent")]
        public async Task<IActionResult> RecordConsent([FromBody] ConsentRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.RecordConsentAsync(userId, request.Type, request.Accepted, request.Version);
            return Ok(new { success });
        }

        [Authorize]
        [HttpGet("consent")]
        public async Task<IActionResult> GetConsentStatus()
        {
            var userId = GetCurrentUserId();
            var status = await _userService.GetConsentStatusAsync(userId);
            return Ok(status);
        }

        [Authorize]
        [HttpPost("age-verification")]
        public async Task<IActionResult> VerifyAge([FromBody] AgeVerificationRequest request)
        {
            var userId = GetCurrentUserId();
            var success = await _userService.VerifyAgeAsync(userId, request.IsAdult);
            return Ok(new { success });
        }

        [HttpGet("config/public")]
        public async Task<IActionResult> GetPublicConfig()
        {
            // Would return public game config including privacy policy URL, terms URL, etc.
            var config = new
            {
                PrivacyPolicyUrl = "https://goddessrequiem.com/privacy",
                TermsOfServiceUrl = "https://goddessrequiem.com/terms",
                MinimumAge = 13,
                SupportedLanguages = Enum.GetNames(typeof(Language)),
                AppVersion = "1.0.0",
                MaintenanceMode = false
            };
            return Ok(config);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst("userId");
            return Guid.Parse(claim.Value);
        }
    }

    // Request DTOs
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public Platform Platform { get; set; }
        public string DeviceId { get; set; }
        public Language Language { get; set; } = Language.Portuguese;
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string DeviceId { get; set; }
    }

    public class DeviceLoginRequest
    {
        public string DeviceId { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class VerifyEmailRequest
    {
        public Guid UserId { get; set; }
        public string Token { get; set; }
    }

    public class PasswordResetRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class UpdateDisplayNameRequest
    {
        public string DisplayName { get; set; }
    }

    public class UpdateLanguageRequest
    {
        public Language Language { get; set; }
    }

    public class UpdateSettingsRequest
    {
        public Dictionary<string, object> Settings { get; set; }
    }

    public class DeleteAccountRequest
    {
        public string Password { get; set; }
    }

    public class ConsentRequest
    {
        public ConsentType Type { get; set; }
        public bool Accepted { get; set; }
        public string Version { get; set; }
    }

    public class AgeVerificationRequest
    {
        public bool IsAdult { get; set; }
    }
}