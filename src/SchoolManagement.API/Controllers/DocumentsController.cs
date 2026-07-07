using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Documents.DTOs;
using SchoolManagement.Application.Documents.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Documents}")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ICurrentUserService _currentUser;

    public DocumentsController(IDocumentService documentService, ICurrentUserService currentUser)
    {
        _documentService = documentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StudentDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var documents = await _documentService.ListAsync(schoolId, studentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StudentDocumentDto>>.Ok(documents));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<StudentDocumentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(
        [FromForm] Guid studentId,
        [FromForm] string documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Fichier vide."));
        }

        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await using var stream = file.OpenReadStream();
        var document = await _documentService.UploadAsync(
            schoolId,
            new UploadStudentDocumentRequest(studentId, documentType),
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            cancellationToken);

        return Created(string.Empty, ApiResponse<StudentDocumentDto>.Ok(document, "Document enregistré."));
    }

    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = Permissions.StudentsRead)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var (stream, fileName, mimeType) = await _documentService.DownloadAsync(schoolId, id, cancellationToken);
        return File(stream, mimeType, fileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _documentService.DeleteAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Document supprimé."));
    }
}
