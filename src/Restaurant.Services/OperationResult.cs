namespace Restaurant.Services;

/// <summary>Результат бизнес-операции: признак успеха и сообщение для пользователя.</summary>
public class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";

    public static OperationResult Ok(string message = "") => new() { Success = true, Message = message };
    public static OperationResult Fail(string message) => new() { Success = false, Message = message };
}
