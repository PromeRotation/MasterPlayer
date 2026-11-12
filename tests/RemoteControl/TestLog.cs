namespace MasterPlayer.RemoteControlTests;

public sealed class TestLog
{
    public void Warning(Exception exception, string message)
    {
        // Expected rejection paths are asserted through the client's error state.
    }
}
