using ComputerLending.Core.Enums;

namespace ComputerLending.Core.Results;

public class Result
{
    public ResultStatus Status { get; protected init; }
    public string? Message { get; protected init; }
    public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent;

    public static Result Ok() => new() { Status = ResultStatus.Ok };
    public static Result NoContent() => new() { Status = ResultStatus.NoContent };
    public static Result Validation(string message) => new() { Status = ResultStatus.Validation, Message = message };
    public static Result Unauthorized(string message) => new() { Status = ResultStatus.Unauthorized, Message = message };
    public static Result Forbidden(string message) => new() { Status = ResultStatus.Forbidden, Message = message };
    public static Result NotFound(string message) => new() { Status = ResultStatus.NotFound, Message = message };
    public static Result Conflict(string message) => new() { Status = ResultStatus.Conflict, Message = message };

    public static Result<T> Ok<T>(T value) => Result<T>.Success(value, ResultStatus.Ok);
    public static Result<T> Created<T>(T value) => Result<T>.Success(value, ResultStatus.Created);
}

public class Result<T> : Result
{
    public T? Value { get; private init; }

    internal static Result<T> Success(T value, ResultStatus status) =>
        new() { Status = status, Value = value };

    public static new Result<T> Validation(string message) =>
        new() { Status = ResultStatus.Validation, Message = message };

    public static new Result<T> Unauthorized(string message) =>
        new() { Status = ResultStatus.Unauthorized, Message = message };

    public static new Result<T> Forbidden(string message) =>
        new() { Status = ResultStatus.Forbidden, Message = message };

    public static new Result<T> NotFound(string message) =>
        new() { Status = ResultStatus.NotFound, Message = message };

    public static new Result<T> Conflict(string message) =>
        new() { Status = ResultStatus.Conflict, Message = message };
}
