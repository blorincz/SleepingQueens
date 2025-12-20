namespace SleepingQueens.Tests.Helpers;

public static class TestConstants
{
    public const string TestConnectionId = "test-connection-123";
    public const string TestPlayerName = "Test Player";
    public const string TestGameCode = "TEST01";

    public static class ConnectionStrings
    {
        public const string InMemory = "InMemory";
        public const string TestDatabase = "TestDatabase";
    }

    public static class Timeouts
    {
        public static readonly TimeSpan HubMessageTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan ApiCallTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan DatabaseOperationTimeout = TimeSpan.FromSeconds(5);
    }
}