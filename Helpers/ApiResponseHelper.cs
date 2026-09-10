using System;
using Microsoft.AspNetCore.Mvc;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Standardized helper for API responses with clean separation of user-facing and developer-facing errors.
    /// </summary>
    public static class ApiResponseHelper
    {
        /// <summary>
        /// Creates a standardized error response object.
        /// userError: User-friendly message displayed on client UI toasts/screens.
        /// developerError: Detailed technical exception / database error for debugging in API responses & console.
        /// </summary>
        public static object Error(string userError, Exception? ex = null)
        {
            var devError = ex?.InnerException?.Message ?? ex?.Message ?? userError;
            return new
            {
                userError = userError,
                developerError = devError,
                message = userError,
                detail = devError
            };
        }

        /// <summary>
        /// Creates a standardized error response object with custom developer message.
        /// </summary>
        public static object Error(string userError, string developerError)
        {
            return new
            {
                userError = userError,
                developerError = developerError,
                message = userError,
                detail = developerError
            };
        }

        /// <summary>
        /// Creates a standardized 500 Internal Server Error ObjectResult.
        /// </summary>
        public static ObjectResult InternalServerError(string userError, Exception? ex = null)
        {
            return new ObjectResult(Error(userError, ex)) { StatusCode = 500 };
        }

        /// <summary>
        /// Creates a standardized 400 Bad Request ObjectResult.
        /// </summary>
        public static BadRequestObjectResult BadRequestError(string userError, string? developerError = null)
        {
            return new BadRequestObjectResult(Error(userError, developerError ?? userError));
        }

        /// <summary>
        /// Creates a standardized 404 Not Found ObjectResult.
        /// </summary>
        public static NotFoundObjectResult NotFoundError(string userError, string? developerError = null)
        {
            return new NotFoundObjectResult(Error(userError, developerError ?? userError));
        }
    }
}
