namespace BalanceControl.Api.Controllers.Base.BaseApiResponse;

[Serializable]
public class ApiResponse<T>
{
    public T Data { get; set; } = default!;
    public List<ApiErrorDetail> Errors { get; set; } = [];

    public static ApiResponse<T> Success(T data) => new() { Data = data };

    public static ApiResponse<T> Failure(List<ApiErrorDetail> errors, T? data = default) => new()
    {
        Data = data ?? default!,
        Errors = errors
    };

    public static ApiResponse<T> SingleFailure(Int16 code, string message, object? details = null) => new()
    {
        Data = default!,
        Errors = [new ApiErrorDetail { Code = code, Message = message, Details = details }]
    };
}
