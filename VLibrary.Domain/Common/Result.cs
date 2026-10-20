namespace VLibrary.Domain.Common
{
    /// <summary>
    /// Lets generic code (e.g. pipeline behaviors) create a failed result of a concrete result type.
    /// </summary>
    public interface IFailureFactory<TSelf> where TSelf : IFailureFactory<TSelf>
    {
        static abstract TSelf FromError(Error error);
    }

    public class Result : IFailureFactory<Result>
    {
        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
            {
                throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
            }

            if (!isSuccess && error == Error.None)
            {
                throw new ArgumentException("A failed result must carry an error.", nameof(error));
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);
        public static Result<T> Success<T>(T value) => new(value, true, Error.None);
        public static Result<T> Failure<T>(Error error) => new(default, false, error);

        public static Result FromError(Error error) => Failure(error);

        public static implicit operator Result(Error error) => Failure(error);
    }

    public sealed class Result<T> : Result, IFailureFactory<Result<T>>
    {
        private readonly T? _value;

        internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
        {
            _value = value;
        }

        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

        public static new Result<T> FromError(Error error) => Failure<T>(error);

        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(Error error) => Failure<T>(error);
    }
}
