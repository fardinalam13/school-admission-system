using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
[Produces("application/json")]
public sealed class ClassesController(AdmissionService service) : ControllerBase
{
    /// <summary>POST /classes — school admin creates a class (one per shift) with a seat quota.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClassCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClassCreatedResponse>> Create([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        // [ApiController] has already rejected missing/invalid fields with 400 before we get here.
        var created = await service.CreateClassAsync(request.Name!, request.TotalSeats!.Value, ct);
        return CreatedAtAction(nameof(GetById), new { classId = created.Id }, created);
    }

    /// <summary>GET /classes — every class with live seat accounting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSeatsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSeatsResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListClassesAsync(ct));

    /// <summary>GET /classes/{classId} — seat accounting for one class.</summary>
    [HttpGet("{classId:guid}")]
    [ProducesResponseType(typeof(ClassSeatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassSeatsResponse>> GetById(Guid classId, CancellationToken ct) =>
        Ok(await service.GetClassAsync(classId, ct));
}
