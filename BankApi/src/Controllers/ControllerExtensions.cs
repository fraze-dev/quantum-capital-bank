using BankApi.Dtos;
using BankApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

public static class ControllerExtensions
{
    // Turns a failed service operation into the matching HTTP response.
    public static IActionResult ErrorResult(this ControllerBase controller, ServiceError error, string message) => error switch
    {
        ServiceError.NotFound => controller.NotFound(message),
        ServiceError.Conflict => controller.Conflict(message),
        _ => controller.BadRequest(message)
    };

    // Status changes must say why (it goes in the audit log). Returns an
    // error message, or null if the request is usable.
    public static string ValidateStatusChange(StatusChangeRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return "Status is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "A reason is required.";
        }

        if (request.Reason.Trim().Length > StatusChangeRequest.MaxReasonLength)
        {
            return $"Reason can be at most {StatusChangeRequest.MaxReasonLength} characters.";
        }

        return null;
    }
}
