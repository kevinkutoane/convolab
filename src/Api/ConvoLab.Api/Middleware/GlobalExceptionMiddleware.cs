using ConvoLab.Application.Common.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception exception)
        {
            var problem = Map(exception, context.TraceIdentifier);
            if (problem.Status >= 500)
                logger.LogError(exception, "Unhandled request failure {ErrorCode}", problem.Extensions["code"]);
            else
                logger.LogWarning(exception, "Request rejected {ErrorCode}", problem.Extensions["code"]);

            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json");
        }
    }

    private static ProblemDetails Map(Exception exception, string correlationId)
    {
        (int status, string title, string detail, string code, IReadOnlyDictionary<string, string[]> errors) mapped = exception switch
        {
            AntiforgeryValidationException => (
                StatusCodes.Status400BadRequest,
                "Antiforgery validation failed",
                "The request verification token is missing, invalid, or expired. Refresh and retry.",
                "auth.antiforgery.invalid",
                EmptyErrors()),
            RequestValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                validation.Message,
                validation.Code,
                validation.ValidationErrors),
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "One or more request values are invalid.",
                "validation.failed",
                validation.Errors
                    .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "request" : error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
            ResourceNotFoundException missing => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                missing.Message,
                missing.Code,
                missing.ValidationErrors),
            ResourceConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Conflict",
                conflict.Message,
                conflict.Code,
                conflict.ValidationErrors),
            ConcurrencyConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                conflict.Message,
                conflict.Code,
                conflict.ValidationErrors),
            DomainRuleViolationException rule => (
                StatusCodes.Status422UnprocessableEntity,
                "Domain rule violation",
                rule.Message,
                rule.Code,
                rule.ValidationErrors),
            CapabilityUnavailableException unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "Capability unavailable",
                unavailable.Message,
                unavailable.Code,
                unavailable.ValidationErrors),
            ExternalDependencyException dependency => (
                StatusCodes.Status502BadGateway,
                "External dependency failure",
                dependency.Message,
                dependency.Code,
                dependency.ValidationErrors),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                "The requested resource was not found.",
                "resource.not_found",
                EmptyErrors()),
            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                argument.Message,
                "request.invalid",
                EmptyErrors()),
            InvalidOperationException operation => (
                StatusCodes.Status422UnprocessableEntity,
                "Operation rejected",
                operation.Message,
                "operation.rejected",
                EmptyErrors()),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected platform error",
                "The platform could not complete the request. Use the correlation id when reviewing server logs.",
                "platform.unexpected_error",
                EmptyErrors())
        };

        var problem = new ProblemDetails
        {
            Status = mapped.status,
            Title = mapped.title,
            Detail = mapped.detail,
            Type = $"https://errors.convolab.dev/{mapped.code}",
            Instance = correlationId
        };
        problem.Extensions["code"] = mapped.code;
        problem.Extensions["correlationId"] = correlationId;
        if (mapped.errors.Count > 0) problem.Extensions["errors"] = mapped.errors;
        return problem;
    }

    private static IReadOnlyDictionary<string, string[]> EmptyErrors()
        => new Dictionary<string, string[]>();
}
