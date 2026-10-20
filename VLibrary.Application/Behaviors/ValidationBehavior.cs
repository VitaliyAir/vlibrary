using FluentValidation;
using MediatR;
using VLibrary.Domain.Common;

namespace VLibrary.Application.Behaviors
{
    /// <summary>
    /// Runs the FluentValidation validators of a request and turns failures into a validation <see cref="Error"/>.
    /// </summary>
    internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result, IFailureFactory<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!validators.Any())
            {
                return await next(cancellationToken);
            }

            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count == 0)
            {
                return await next(cancellationToken);
            }

            var details = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

            return TResponse.FromError(Error.ValidationFailed(details));
        }
    }
}
