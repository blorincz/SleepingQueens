using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Tests.Helpers;

public static class TestExtensions
{
    public static void ShouldBeCloseTo(this DateTime actual, DateTime expected, TimeSpan tolerance)
    {
        actual.Should().BeCloseTo(expected, tolerance);
    }

    public static Mock<HttpContext> CreateMockHttpContext(string connectionId = "test-connection")
    {
        var mockHttpContext = new Mock<HttpContext>();
        var mockConnection = new Mock<Microsoft.AspNetCore.Http.ConnectionInfo>();
        var mockRequest = new Mock<HttpRequest>();
        var mockResponse = new Mock<HttpResponse>();
        var mockWebSocketManager = new Mock<WebSocketManager>();

        mockConnection.Setup(c => c.Id).Returns(connectionId);
        mockHttpContext.Setup(c => c.Connection).Returns(mockConnection.Object);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        mockHttpContext.Setup(c => c.Response).Returns(mockResponse.Object);
        mockHttpContext.Setup(c => c.WebSockets).Returns(mockWebSocketManager.Object);

        return mockHttpContext;
    }

    public static List<T> AsList<T>(this T item)
    {
        return new List<T> { item };
    }

    public static T WithRandomId<T>(this T obj) where T : class
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(Guid))
        {
            idProperty.SetValue(obj, Guid.NewGuid());
        }
        return obj;
    }

    // Extension for converting string to QueenType
    public static QueenType ToQueenType(this string queenTypeString)
    {
        if (Enum.TryParse<QueenType>(queenTypeString, out var queenType))
        {
            return queenType;
        }

        // Default to RoseQueen if parsing fails
        return QueenType.RoseQueen;
    }

    public static string ToQueenTypeString(this QueenType queenType)
    {
        return queenType.ToString();
    }
}