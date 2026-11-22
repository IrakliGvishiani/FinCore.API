using FinCore.Application.Exceptions;
using FinCore.Domain.DomainExceptions;
using FinCore.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinCore.Tests.Api;

public class ErrorHandlingMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<ErrorHandlingMiddleware>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, exception, formatter(state, exception)));
    }

    private static async Task<(int Status, JsonElement Body, CapturingLogger Logger)> Run(Exception toThrow)
    {
        var logger = new CapturingLogger();
        var middleware = new ErrorHandlingMiddleware(_ => throw toThrow, logger);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement.Clone(), logger);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithGenericMessage_AndLogsTheRealException()
    {
        var secret = new InvalidOperationException("Ledger account CARDUSD is not configured; conn=internal-db-01");

        var (status, body, logger) = await Run(secret);

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
        body.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.GetProperty("statusCode").GetInt32().Should().Be(500);
        body.GetRawText().Should().NotContain("CARDUSD").And.NotContain("internal-db-01");

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && ReferenceEquals(e.Exception, secret));
    }

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public async Task KnownExceptions_KeepTheirMessage_AndAreNotLoggedAsErrors(Exception ex, int expectedStatus)
    {
        var (status, body, logger) = await Run(ex);

        status.Should().Be(expectedStatus);
        body.GetProperty("message").GetString().Should().Be(ex.Message);
        logger.Entries.Should().BeEmpty();
    }

    public static IEnumerable<object[]> KnownExceptions() => new[]
    {
        new object[] { new BadRequestException("Amount must be positive with at most 2 decimals."), 400 },
        new object[] { new NotFoundException("Virtual card not found"), 404 },
        new object[] { new ConflictException("The charged amount has changed."), 409 },
        new object[] { new ExternalServiceException("Exchange rates are temporarily unavailable."), 503 },
        new object[] { new DomainException("Closed cards cannot be changed."), 400 },
    };
}
