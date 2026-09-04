using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/auth/otp")]
    public class OtpApiController : ControllerBase
    {
        private readonly OtpService _otpService;
        private readonly AuthBal _authBal;

        public OtpApiController(OtpService otpService, AuthBal authBal)
        {
            _otpService = otpService;
            _authBal = authBal;
        }

        public class SendOtpRequest
        {
            public string? PhoneNumber { get; set; }
        }

        public class VerifyOtpRequest
        {
            public string? PhoneNumber { get; set; }
            public string? Otp { get; set; }
            public string? FullName { get; set; }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
            {
                return BadRequest(new { success = false, message = "Phone number is required." });
            }

            var result = await _otpService.GenerateAndSendOtpAsync(request.PhoneNumber);
            if (!result.Success)
            {
                return StatusCode(429, new
                {
                    success = false,
                    message = result.Message,
                    cooldownSeconds = result.CooldownSeconds
                });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                cooldownSeconds = result.CooldownSeconds,
                demoOtp = result.DemoOtp
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.PhoneNumber) || string.IsNullOrWhiteSpace(request?.Otp))
            {
                return BadRequest(new { success = false, message = "Phone number and verification code are required." });
            }

            var verifyResult = await _otpService.VerifyOtpAsync(request.PhoneNumber, request.Otp);
            if (!verifyResult.Success)
            {
                return BadRequest(new { success = false, message = verifyResult.Message });
            }

            var cleanPhone = _otpService.NormalizePhoneNumber(request.PhoneNumber);
            var user = await _authBal.GetOrCreateUserByPhoneAsync(cleanPhone, request.FullName);
            if (user == null)
            {
                return StatusCode(500, new { success = false, message = "Failed to establish VIP user session." });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Client")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.Now.AddDays(30)
            });

            return Ok(new
            {
                success = true,
                message = "Authentication successful. Welcome to SAT Jewel VIP Vault.",
                user = new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    email = user.Email,
                    phone = user.Phone
                }
            });
        }
    }
}
