// Generated from GTFO.EZ src/GTFO.EZ/Shared/WardenMessage.cs (dc46943) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace MindAttic.GTFO.Shared;

// One warden message shown for any GTFO.* mod (a toggle, a gear swap). A new one replaces one of
// ours still on screen instead of queueing behind it. "Ours is on screen" is kept once for the whole
// process, so two mods' messages replace each other rather than each stopping the panel on its own
// stale say-so. It only counts when the panel's running message is the one ours started (one queued
// behind a game message never started, so a second press can't cut the game's message short), when
// nothing of the game's is queued behind it (resetting the queue would drop that), and until the
// message would have finished (StaleAfter), so a message dropped by a level change never leaves it on.
internal static class WardenMessage
{
    private const string UpUntilKey = "MindAttic.GTFO.WardenMessageUpUntil";
    private const string RoutineKey = "MindAttic.GTFO.WardenMessageRoutine";
    private const float RevealSpeed = 200f;
    private const float HoldSeconds = 5f;
    private const float StaleAfter = HoldSeconds + 3f;

    private static float UpUntil
    {
        get => AppDomain.CurrentDomain.GetData(UpUntilKey) is float t ? t : 0f;
        set => AppDomain.CurrentDomain.SetData(UpUntilKey, value);
    }

    // The panel coroutine our latest message started, by native pointer (the same in every mod's copy).
    private static IntPtr Routine
    {
        get => AppDomain.CurrentDomain.GetData(RoutineKey) is IntPtr p ? p : IntPtr.Zero;
        set => AppDomain.CurrentDomain.SetData(RoutineKey, value);
    }

    // Returns false when there's no warden panel to show it on (callers log it instead).
    internal static bool Show(string message)
    {
        var intel = GuiManager.PlayerLayer?.m_wardenIntel;
        if (intel == null) return false;

        var running = intel.m_newObjectiveRoutine;
        var queued = intel.m_subObjectiveMessageQueue;
        if (Time.unscaledTime < UpUntil && running != null && running.Pointer == Routine && (queued == null || queued.Count == 0))
        {
            intel.StopCoroutine(running);
            if (intel.m_revealTextRoutine != null) intel.StopCoroutine(intel.m_revealTextRoutine);
            intel.m_newObjectiveRoutine = null;
            intel.m_revealTextRoutine = null;
            intel.ResetSubObjectiveMesssagQueue();
        }

        var before = intel.m_newObjectiveRoutine?.Pointer ?? IntPtr.Zero;
        var until = Time.unscaledTime + StaleAfter;
        intel.ShowSubObjectiveMessage("", message, false, RevealSpeed, HoldSeconds,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
            {
                // Only the latest message clears the flag; an older one finishing late leaves it.
                if (UpUntil == until) UpUntil = 0f;
            })));

        // Started straight away (a new routine on the panel): ours is on screen and the next one may
        // replace it. Queued behind another message: it isn't, so the next one queues too.
        var after = intel.m_newObjectiveRoutine?.Pointer ?? IntPtr.Zero;
        if (after != IntPtr.Zero && after != before)
        {
            Routine = after;
            UpUntil = until;
        }
        else
        {
            Routine = IntPtr.Zero;
            UpUntil = 0f;
        }
        return true;
    }
}
