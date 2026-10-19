// Generated from GTFO.EZ src/GTFO.EZ/Shared/RemoteEvents.cs (97eb384) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using GTFO.API;

namespace MindAttic.GTFO.Shared;

// Registers a GTFO-API network event whose handler can't throw. GTFO-API runs every handler inside
// its own [HarmonyWrapSafe] prefix on the game's packet receive (SNet_Replication.RecieveBytes): an
// exception escaping one makes that prefix fall through to the game's own receive, which then reads
// GTFO-API's packet as one of its own. So the handler's error is logged and dropped here instead.
// Only for the GTFO.* mods that use GTFO-API (tools/publish-shared.ps1 publishes it to those alone).
internal static class RemoteEvents
{
    internal static void Register<T>(string eventName, Action<ulong, T> onReceive) where T : struct =>
        NetworkAPI.RegisterEvent<T>(eventName, (sender, message) =>
        {
            try
            {
                onReceive(sender, message);
            }
            catch (Exception e)
            {
                SharedOwner.Log.LogError($"Couldn't handle network event {eventName} from {sender}: {e}");
            }
        });
}
