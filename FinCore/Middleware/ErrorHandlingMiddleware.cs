using FinCore.Application.Exceptions;
using FinCore.Domain.DomainExceptions;
using System.Text.Json;

namespace FinCore.Middleware
{
    public class ErrorHandlingMiddleware(RequestDelegate next,ILogger<ErrorHandlingMiddleware> logger)
    {

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch(Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var statusCode = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                BadRequestException => StatusCodes.Status400BadRequest,
                NotAllowedException => StatusCodes.Status405MethodNotAllowed,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ConcurrencyConflictException => StatusCodes.Status409Conflict,
                DuplicateKeyException => StatusCodes.Status409Conflict,
                ConflictException => StatusCodes.Status409Conflict,
                ExternalServiceException => StatusCodes.Status503ServiceUnavailable,
                InsufficientFundsException => StatusCodes.Status400BadRequest,
                DomainException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
            if (statusCode == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception");

            var message = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : ex.Message;


            // 500s never leak internal details; the real exception is logged above.
            var response = new CommonResponse(message,null,false,statusCode);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var json = JsonSerializer.Serialize(response,new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
