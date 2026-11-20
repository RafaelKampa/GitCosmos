using System.Text.Json.Serialization;

namespace GitCosmos.Application.Common;

public class CustomResponse<T>
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Messages { get; set; } = new();
    public T? Result { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalRows { get; set; }

    public CustomResponse() { }

    public CustomResponse(bool success, List<string> errors, T? result, int? totalRows = null, List<string>? messages = null)
    {
        Success = success;
        Errors = errors;
        Messages = messages ?? new List<string>();
        Result = result;
        TotalRows = totalRows;
    }

    public static CustomResponse<T> Ok(T? result, int? totalRows = null) =>
        new(true, new List<string>(), result, totalRows);

    public static CustomResponse<T> Ok(T? result, string message, int? totalRows = null) =>
        new(true, new List<string>(), result, totalRows, new List<string> { message });

    public static CustomResponse<T> Fail(List<string> errors, T? result = default) =>
        new(false, errors, result);

    public static CustomResponse<T> Fail(string error) =>
        new(false, new List<string> { error }, default);

    public static CustomResponse<List<T>> OkList(List<T> items, int totalRows) =>
        new CustomResponse<List<T>>(true, new List<string>(), items, totalRows);

    public static CustomResponse<List<T>> FailList(List<string> errors) =>
        new CustomResponse<List<T>>(false, errors, new List<T>());

    public static CustomResponse<List<T>> FailList(string error) =>
        new CustomResponse<List<T>>(false, new List<string> { error }, new List<T>());
}
