using Xunit;

namespace SleepingQueens.Tests;

public class SimpleTest
{
    [Fact]
    public void BasicTest_ShouldPass()
    {
        // Arrange
        var expected = 4;

        // Act
        var actual = 2 + 2;

        // Assert
        Assert.Equal(expected, actual);
    }
}