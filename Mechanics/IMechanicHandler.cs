using System;

namespace MasterPlayer.Mechanics;

public interface IMechanicHandler : IDisposable
{
    string Id { get; }

    string DisplayName { get; }

    bool Enabled { get; set; }

    void Attach(MechanicDispatcher dispatcher);
}
