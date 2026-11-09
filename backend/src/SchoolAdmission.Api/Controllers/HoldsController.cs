using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class HoldsController(AdmissionService service) : ControllerBase
{
    /// <summary>POST /classes/{classId}/holds — apply for a seat; holds it until expiresAt.</summary>
    [HttpPost("classes/{classId:guid}/holds")]
    [ProducesResponseType(typeof(HoldCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldCreatedResponse>> Create(Guid classId, CancellationToken ct)
    {
        var studentId = StudentIdentity.Require(Request);
        var created = await service.CreateHoldAsync(classId, studentId, ct);
        return CreatedAtAction(nameof(GetById), new { holdId = created.HoldId }, created);
    }

    /// <summary>GET /holds/{holdId} — current state of one application.</summary>
    [HttpGet("holds/{holdId:guid}")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HoldResponse>> GetById(Guid holdId, CancellationToken ct) =>
        Ok(await service.GetHoldAsync(holdId, ct));

    /// <summary>POST /holds/{holdId}/confirm — admission fee paid; idempotent.</summary>
    [HttpPost("holds/{holdId:guid}/confirm")]
    [ProducesResponseType(typeof(HoldConfirmedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldConfirmedResponse>> Confirm(Guid holdId, CancellationToken ct)
    {
        var studentId = StudentIdentity.Require(Request);
        return Ok(await service.ConfirmHoldAsync(holdId, studentId, ct));
    }

    /// <summary>DELETE /holds/{holdId} — withdraw the application; 204 on success.</summary>
    [HttpDelete("holds/{holdId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid holdId, CancellationToken ct)
    {
        var studentId = StudentIdentity.Require(Request);
        await service.CancelHoldAsync(holdId, studentId, ct);
        return NoContent();
    }

    /// <summary>GET /me/holds — every application of the student named in X-Student-Id.</summary>
    [HttpGet("me/holds")]
    [ProducesResponseType(typeof(IReadOnlyList<HoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<HoldResponse>>> Mine(CancellationToken ct)
    {
        var studentId = StudentIdentity.Require(Request);
        return Ok(await service.ListMyHoldsAsync(studentId, ct));
    }
}
