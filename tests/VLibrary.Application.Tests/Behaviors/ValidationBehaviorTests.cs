using FluentValidation;
using MediatR;
using VLibrary.Application.Behaviors;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Common;

namespace VLibrary.Application.Tests.Behaviors
{
    public class ValidationBehaviorTests
    {
        [Fact]
        public async Task InvalidRequest_ReturnsValidationError_WithoutCallingHandler()
        {
            var behavior = new ValidationBehavior<CreateImportCommand, Result<long>>([new CreateImportValidator()]);
            var handlerCalled = false;

            var result = await behavior.Handle(
                new CreateImportCommand("", 1, null),
                _ =>
                {
                    handlerCalled = true;
                    return Task.FromResult<Result<long>>(1L);
                },
                CancellationToken.None);

            handlerCalled.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Validation);
            result.Error.Details.Should().ContainKey(nameof(CreateImportCommand.SourceName));
        }

        [Fact]
        public async Task ValidRequest_CallsHandler()
        {
            IValidator<CreateImportCommand>[] validators = [new CreateImportValidator()];
            var behavior = new ValidationBehavior<CreateImportCommand, Result<long>>(validators);

            var result = await behavior.Handle(
                new CreateImportCommand("Library/Math", 3, null),
                _ => Task.FromResult<Result<long>>(42L),
                CancellationToken.None);

            result.Value.Should().Be(42);
        }
    }
}
