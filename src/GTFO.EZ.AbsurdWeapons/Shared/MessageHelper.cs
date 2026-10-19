// Generated from GTFO.EZ src/GTFO.EZ/Shared/MessageHelper.cs (60ccc72) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace MindAttic.GTFO.Shared;

// Where an on-screen message for the local player goes.
internal enum MessageType
{
    // The warden intel panel, top of the screen (a sub-objective message). Ours replace each other;
    // the game's own are never cut short.
    Warden,
    // The scrolling event/chat feed on the left. Messages stack and fade on their own; local only.
    EventLog,
    // The short line under the crosshair (where "Requires key" shows), held for `seconds`.
    Center,
    // The subtitle line at the bottom, held for `seconds`.
    Subtitle,
}

// One way for any GTFO.* mod to tell the local player something, wherever it fits: Show(text, type).
// Every type returns false when its part of the HUD isn't there (no level, a menu) - callers log the
// text instead - and never throws.
internal static class MessageHelper
{
    internal const float DefaultSeconds = 5f;

    internal static bool Show(string text, MessageType type = MessageType.Warden, float seconds = DefaultSeconds)
    {
        try
        {
            seconds = Mathf.Max(0.5f, seconds);
            return type switch
            {
                MessageType.Warden => Warden(text, seconds),
                MessageType.EventLog => EventLog(text),
                MessageType.Center => Center(text, seconds),
                MessageType.Subtitle => Subtitle(text, seconds),
                _ => false,
            };
        }
        catch (Exception e)
        {
            SharedOwner.Log.LogInfo($"{text} (no {type} message: {e.Message})");
            return false;
        }
    }

    private static bool EventLog(string text)
    {
        var log = GuiManager.PlayerLayer?.m_gameEventLog;
        if (log == null) return false;
        log.AddLogItem(text, eGameEventChatLogType.GameEvent);
        return true;
    }

    // Priority 0 is the lowest, so the game's own interaction messages still win this line.
    private static bool Center(string text, float seconds)
    {
        var layer = GuiManager.InteractionLayer;
        if (layer == null) return false;
        layer.SetTimedMessage(text, seconds, ePUIMessageStyle.Default, 0);
        return true;
    }

    private static bool Subtitle(string text, float seconds)
    {
        var subtitles = GuiManager.PlayerLayer?.m_subtitles;
        if (subtitles == null) return false;
        subtitles.ShowSingleLineSubtitle(text, seconds);
        return true;
    }

    // ── Warden ──────────────────────────────────────────────────────────────
    // A new one replaces one of ours still on screen instead of queueing behind it. "Ours is on
    // screen" is kept once for the whole process, so two mods' messages replace each other rather
    // than each stopping the panel on its own stale say-so. It only counts when the panel's running
    // message is the one ours started (one queued behind a game message never started, so a second
    // press can't cut the game's message short), when nothing of the game's is queued behind it
    // (resetting the queue would drop that), and until the message would have finished, so a
    // message dropped by a level change never leaves it on. (The keys keep their old names so a mod
    // still carrying the earlier WardenMessage copy shares the same state.)
    private const string UpUntilKey = "MindAttic.GTFO.WardenMessageUpUntil";
    private const string RoutineKey = "MindAttic.GTFO.WardenMessageRoutine";
    private const float RevealSpeed = 200f;
    private const float StaleMargin = 3f;

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

    private static bool Warden(string text, float seconds)
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
        var until = Time.unscaledTime + seconds + StaleMargin;
        intel.ShowSubObjectiveMessage("", text, false, RevealSpeed, seconds,
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
