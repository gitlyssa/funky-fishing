using System.Collections.Generic;
using UnityEngine;

public static class JoyConMenuInput
{
    private static readonly Dictionary<int, int> lastButtonsByHandle = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> currentButtonsByHandle = new Dictionary<int, int>();
    private static readonly int[] emptyHandles = System.Array.Empty<int>();

    private static int[] handles = emptyHandles;
    private static int lastPollFrame = -1;
    private static float nextReconnectTime = -1f;
    private static bool jslAvailable = true;
    private static bool initialized;

    private static bool anyConnected;
    private static bool submitPressedThisFrame;
    private static bool pausePressedThisFrame;
    private static Vector2 strongestStick;

    private const float reconnectIntervalNoDevices = 4f;
    private const float reconnectIntervalDisconnected = 1.5f;

    public static bool AnyConnected
    {
        get
        {
            Poll();
            return anyConnected;
        }
    }

    public static Vector2 NavigationStick
    {
        get
        {
            Poll();
            return strongestStick;
        }
    }

    public static bool SubmitPressedThisFrame
    {
        get
        {
            Poll();
            return submitPressedThisFrame;
        }
    }

    public static bool PausePressedThisFrame
    {
        get
        {
            Poll();
            return pausePressedThisFrame;
        }
    }

    private static void Poll()
    {
        if (lastPollFrame == Time.frameCount)
            return;

        lastPollFrame = Time.frameCount;
        submitPressedThisFrame = false;
        pausePressedThisFrame = false;
        strongestStick = Vector2.zero;
        anyConnected = false;

        if (!jslAvailable)
            return;

        if (!initialized)
        {
            initialized = true;
            ReconnectHandles();
        }

        if (handles.Length == 0)
        {
            currentButtonsByHandle.Clear();
            lastButtonsByHandle.Clear();
            if (Time.unscaledTime >= nextReconnectTime)
                ReconnectHandles();
            return;
        }

        currentButtonsByHandle.Clear();
        float strongestSq = 0f;

        for (int i = 0; i < handles.Length; i++)
        {
            int handle = handles[i];
            bool stillConnected;
            try
            {
                stillConnected = handle >= 0 && JSL.JslStillConnected(handle);
            }
            catch
            {
                jslAvailable = false;
                handles = emptyHandles;
                currentButtonsByHandle.Clear();
                lastButtonsByHandle.Clear();
                strongestStick = Vector2.zero;
                anyConnected = false;
                submitPressedThisFrame = false;
                pausePressedThisFrame = false;
                return;
            }

            if (!stillConnected)
                continue;

            anyConnected = true;
            JSL.JOY_SHOCK_STATE state;
            try
            {
                state = JSL.JslGetSimpleState(handle);
            }
            catch
            {
                continue;
            }
            currentButtonsByHandle[handle] = state.buttons;

            Vector2 left = new Vector2(state.stickLX, state.stickLY);
            Vector2 right = new Vector2(state.stickRX, state.stickRY);
            Vector2 candidate = left.sqrMagnitude >= right.sqrMagnitude ? left : right;
            float candidateSq = candidate.sqrMagnitude;
            if (candidateSq > strongestSq)
            {
                strongestSq = candidateSq;
                strongestStick = candidate;
            }

            int previousButtons = lastButtonsByHandle.TryGetValue(handle, out int prev) ? prev : 0;
            bool submitNow = IsConfirmPressed(state.buttons);
            bool submitPrev = IsConfirmPressed(previousButtons);
            if (submitNow && !submitPrev)
                submitPressedThisFrame = true;

            bool pauseNow = IsPausePressed(state.buttons);
            bool pausePrev = IsPausePressed(previousButtons);
            if (pauseNow && !pausePrev)
                pausePressedThisFrame = true;
        }

        lastButtonsByHandle.Clear();
        foreach (KeyValuePair<int, int> kv in currentButtonsByHandle)
            lastButtonsByHandle[kv.Key] = kv.Value;

        if (!anyConnected && Time.unscaledTime >= nextReconnectTime)
            ReconnectHandles();
    }

    private static void ReconnectHandles()
    {
        int count;
        try
        {
            count = JSL.JslConnectDevices();
        }
        catch
        {
            jslAvailable = false;
            handles = emptyHandles;
            nextReconnectTime = Time.unscaledTime + reconnectIntervalNoDevices;
            return;
        }

        if (count <= 0)
        {
            handles = emptyHandles;
            nextReconnectTime = Time.unscaledTime + reconnectIntervalNoDevices;
            return;
        }

        handles = new int[count];
        try
        {
            JSL.JslGetConnectedDeviceHandles(handles, handles.Length);
        }
        catch
        {
            handles = emptyHandles;
            jslAvailable = false;
        }
        nextReconnectTime = Time.unscaledTime + reconnectIntervalDisconnected;
    }

    private static bool IsConfirmPressed(int buttons)
    {
        bool east = IsBitPressed(buttons, JSL.ButtonMaskE);        // Right Joy-Con A
        bool right = IsBitPressed(buttons, JSL.ButtonMaskRight);   // Left Joy-Con Right
        return east || right;
    }

    private static bool IsPausePressed(int buttons)
    {
        return IsBitPressed(buttons, JSL.ButtonMaskPlus) ||
               IsBitPressed(buttons, JSL.ButtonMaskMinus);
    }

    private static bool IsBitPressed(int buttons, int bit)
    {
        return (buttons & (1 << bit)) != 0;
    }
}
