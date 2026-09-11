namespace Journeys.Core.Utility.StreamUtilities
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }
        public string? RawData { get; }

        private Result(bool isSuccess, T? value, string? errorMessage, string? rawData)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
            RawData = rawData;
        }

        public static Result<T> Success(T value) => new Result<T>(true, value, null, null);

        public static Result<T> Error(string errorMessage, string rawData) =>
            new Result<T>(false, default, errorMessage, rawData);
    }
}
