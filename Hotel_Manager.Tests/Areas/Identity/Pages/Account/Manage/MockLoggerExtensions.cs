using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using System;

public static class MockLoggerExtensions
{
    public static ISetup<ILogger<T>> SetupLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel logLevel)
        where T : class
    {
        return loggerMock.Setup(l => l.Log(
            It.Is<LogLevel>(lvl => lvl == logLevel),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
    }

    public static void VerifyLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel logLevel, string message, Times? times = null)
        where T : class
    {
        times ??= Times.Once();

        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == logLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times.Value);
    }
}