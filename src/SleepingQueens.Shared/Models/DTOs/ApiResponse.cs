namespace SleepingQueens.Shared.Models.DTOs;

public class ApiResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResponse SuccessResponse()
    {
        return new ApiResponse { Success = true };
    }

    public static ApiResponse ErrorResponse(string errorMessage)
    {
        return new ApiResponse { Success = false, ErrorMessage = errorMessage };
    }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> SuccessResponse(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data
        };
    }

    public static new ApiResponse<T> ErrorResponse(string errorMessage)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}