using VLibrary.Domain.Common;

namespace VLibrary.Api.Infrastructure
{
    /// <summary>
    /// Maps <see cref="Result"/> to HTTP responses; errors become RFC 7807 ProblemDetails.
    /// </summary>
    public static class ResultExtensions
    {
        public static IResult ToHttp(this Result result) =>
            result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();

        public static IResult ToHttp<T>(this Result<T> result) =>
            result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

        public static IResult ToProblem(this Error error)
        {
            if (error.Type == ErrorType.Validation && error.Details != null)
            {
                return Results.ValidationProblem(
                    error.Details.ToDictionary(),
                    detail: error.Message,
                    title: error.Code);
            }

            var statusCode = error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            return Results.Problem(statusCode: statusCode, title: error.Code, detail: error.Message);
        }
    }
}
