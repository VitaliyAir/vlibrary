using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using VLibrary.Domain.Common;

namespace VLibrary.Application.Behaviors
{
    internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();

            var response = await next(cancellationToken);

            if (response.IsFailure)
            {
                logger.LogWarning("{Request} failed in {ElapsedMs} ms: [{ErrorCode}] {ErrorMessage}",
                    requestName, stopwatch.ElapsedMilliseconds, response.Error.Code, response.Error.Message);
            }
            else
            {
                logger.LogInformation("{Request} handled in {ElapsedMs} ms", requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
    }
}
