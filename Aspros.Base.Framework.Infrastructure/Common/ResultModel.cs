namespace Aspros.Base.Framework.Infrastructure
{
    public class ResultModel
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public static ResultModel Success()
        {
            return new ResultModel { IsSuccess = true };
        }

        public static ResultModel Success<T>(T data)
        {
            return new ResultModel { IsSuccess = true, Data = data };
        }

        public static ResultModel Fail(string message)
        {
            return new ResultModel { IsSuccess = false, Message = message };
        }
    }
}
