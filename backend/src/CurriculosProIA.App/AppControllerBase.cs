using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App;

public abstract class AppControllerBase
{
    protected static IActionResult Ok(object? value) => new OkObjectResult(value);
    protected static IActionResult BadRequest(object? value) => new BadRequestObjectResult(value);
    protected static IActionResult Unauthorized(object? value) => new UnauthorizedObjectResult(value);
    protected static IActionResult NotFound(object? value) => new NotFoundObjectResult(value);
    protected static IActionResult Conflict(object? value) => new ConflictObjectResult(value);
    protected static IActionResult StatusCode(int code, object? value) => new ObjectResult(value) { StatusCode = code };
    protected static IActionResult Content(string content, string contentType) => new ContentResult { Content = content, ContentType = contentType };
    protected static IActionResult Redirect(string url) => new RedirectResult(url);
    protected static IActionResult File(byte[] contents, string contentType, string fileDownloadName) =>
        new FileContentResult(contents, contentType) { FileDownloadName = fileDownloadName };
}
