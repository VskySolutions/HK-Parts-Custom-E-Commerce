using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Authentication;
using HKPartsEcommerce.Application.Features.CustomerAuth;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public storefront customer authentication: registration, email verification, login, and the
/// password-reset flow (REQ-CUS-001).
/// </summary>
[Route("api/customer/auth")]
[AllowAnonymous]
public class CustomerAuthController : ApiControllerBase
{
    /// <summary>Register a new customer account and send an email-verification link.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<Guid>> Register([FromBody] RegisterCustomerCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Express registration from checkout: creates a ready-to-use (email-verified) account from the
    /// delivery details, emails a generated password, and saves the address — so a guest can sign in
    /// and finish the order without re-entering anything.
    /// </summary>
    [HttpPost("register-at-checkout")]
    public async Task<ActionResult<RegisterAtCheckoutResult>> RegisterAtCheckout([FromBody] RegisterCustomerAtCheckoutCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Verify a customer's email address with the token from the verification email.</summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>Authenticate a customer and receive an access + refresh token pair.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] CustomerLoginCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Request a password-reset email. Always succeeds, even for unknown accounts.</summary>
    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>Set a new password using a valid password-reset token.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }
}
