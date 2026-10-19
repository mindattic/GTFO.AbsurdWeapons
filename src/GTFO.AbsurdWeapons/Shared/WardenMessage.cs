// Generated from GTFO.EZ src/GTFO.EZ/Shared/WardenMessage.cs (a2ed1c2) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace MindAttic.GTFO.Shared;

// One warden message shown for any GTFO.* mod (a toggle, a gear swap). A new one replaces one of
// ours still up instead of queueing behind it. "Ours is up" is kept once for the whole process, so
// two mods' messages replace each other rather than each stopping the panel on its own stale say-so;
// and it lapses on its own once the message would have finished (StaleAfter), so a message dropped
// by a level change or a queue reset never leaves it stuck on and cutting the game's own messages.
internal static class WardenMessage
{
    private const string UpUntilKey = "MindAttic.GTFO.WardenMessageUpUntil";
    private const float RevealSpeed = 200f;
    private const float HoldSeconds = 5f;
    private const float StaleAfter = HoldSeconds + 3f;

    private static float UpUntil
    {
        get => AppDomain.CurrentDomain.GetData(UpUntilKey) is float t ? t : 0f;
        set => AppDomain.CurrentDomain.SetData(UpUntilKey, value);
    }

    // Returns false when there's no warden panel to show it on (callers log it instead).
    internal static bool Show(string message)
    {
        var intel = GuiManager.PlayerLayer?.m_wardenIntel;
        if (intel == null) return false;
        if (Time.unscaledTime < UpUntil)
        {
            if (intel.m_newObjectiveRoutine != null) intel.StopCoroutine(intel.m_newObjectiveRoutine);
            if (intel.m_revealTextRoutine != null) intel.StopCoroutine(intel.m_revealTextRoutine);
            intel.m_newObjectiveRoutine = null;
            intel.m_revealTextRoutine = null;
            intel.ResetSubObjectiveMesssagQueue();
        }
        var shownAt = Time.unscaledTime;
        UpUntil = shownAt + StaleAfter;
        intel.ShowSubObjectiveMessage("", message, false, RevealSpeed, HoldSeconds,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
            {
                // Only the latest message clears the flag; an older one finishing late leaves it.
                if (UpUntil == shownAt + StaleAfter) UpUntil = 0f;
            })));
        return true;
    }
}
