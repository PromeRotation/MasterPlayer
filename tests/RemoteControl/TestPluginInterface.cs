using System.Reflection;
using Dalamud.Plugin.Ipc;

namespace MasterPlayer.RemoteControlTests;

public sealed class TestPluginInterface
{
    private static T Create<T>(string channel) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestIpcProxy>();
        ((TestIpcProxy)(object)proxy).Channel = channel;
        return proxy;
    }

    public ICallGateSubscriber<T> GetIpcSubscriber<T>(string channel) => Create<ICallGateSubscriber<T>>(channel);
    public ICallGateSubscriber<T1, T> GetIpcSubscriber<T1, T>(string channel) => Create<ICallGateSubscriber<T1, T>>(channel);
    public ICallGateSubscriber<T1, T2, T> GetIpcSubscriber<T1, T2, T>(string channel) => Create<ICallGateSubscriber<T1, T2, T>>(channel);
    public ICallGateSubscriber<T1, T2, T3, T> GetIpcSubscriber<T1, T2, T3, T>(string channel) => Create<ICallGateSubscriber<T1, T2, T3, T>>(channel);
}
