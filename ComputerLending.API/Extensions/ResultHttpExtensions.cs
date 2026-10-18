using ComputerLending.API.Middleware;
using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Results;
using Microsoft.AspNetCore.Mvc;

namespace ComputerLending.API.Extensions;

public static class ResultHttpExtensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller) =>
        result.Status switch
        {
            ResultStatus.NoContent => controller.NoContent(),
            _ => Status(controller, result.Status, result.Message)
        };

    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        string? createdAtLocation = null)
    {
        return result.Status switch
        {
            ResultStatus.Ok => controller.Ok(result.Value),
            ResultStatus.Created => controller.Created(createdAtLocation ?? string.Empty, result.Value),
            ResultStatus.NoContent => controller.NoContent(),
            _ => Status(controller, result.Status, result.Message)
        };
    }

    private static IActionResult Status(ControllerBase controller, ResultStatus status, string? message)
    {
        var httpStatus = status switch
        {
            ResultStatus.Validation => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var correlationId = controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        return controller.StatusCode(httpStatus, new ErrorResponse
        {
            Status = httpStatus,
            Message = message ?? string.Empty,
            CorrelationId = correlationId
        });
    }
}
