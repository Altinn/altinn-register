using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers
{
    /// <summary>
    /// Handles the presentation of unhandled exceptions during the execution of a request.
    /// </summary>
    [ApiController]
    [ApiVersion(1.0)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [Route("register/api/v{version:apiVersion}/error")]
    public class ErrorController : ControllerBase
    {
        /// <summary>
        /// Create a response with a new <see cref="ProblemDetails"/> instance with limited information.
        /// </summary>
        /// <remarks>
        /// This method cannot be called directly. It is used by the API framework as a way to output ProblemDetails
        /// if there has been an unhandled exception.
        /// </remarks>
        /// <returns>A new <see cref="ObjectResult"/> instance.</returns>
        [Route("")]
        public IActionResult Error() => Problem();
    }
}
