using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class JoyConConnectionService
{
    private static readonly HashSet<string> MenuScenes = new HashSet<string>(StringComparer.Ordinal)
    {
        "MainMenu",
        "ControllerMenu",
        "PondSelect"
    };

    private const float MinScanIntervalSeconds = 0.75f;
    private static int[] _handles = Array.Empty<int>();
    private static bool _scanRequested = true;
    private static float _lastScanAt = -999f;
    private static int _revision;
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _handles = Array.Empty<int>();
        _scanRequested = true;
        _lastScanAt = -999f;
        _revision = 0;
        _initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureInitialized();
        RequestScan();
    }

    public static int[] GetConnectedHandles()
    {
        EnsureInitialized();
        PruneDisconnectedCachedHandles();

        if (_handles.Length == 0)
            _scanRequested = true;

        if (_scanRequested && CanScanNow())
            PerformScan();

        return _handles;
    }

    public static int GetRevision()
    {
        EnsureInitialized();

        if (_scanRequested && CanScanNow())
            PerformScan();

        return _revision;
    }

    public static bool IsHandleConnected(int handle)
    {
        try
        {
            return handle >= 0 && JSL.JslStillConnected(handle);
        }
        catch
        {
            return false;
        }
    }

    public static void RequestScan()
    {
        _scanRequested = true;
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        InputSystem.onDeviceChange += OnDeviceChange;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static bool CanScanNow()
    {
        return (Time.unscaledTime - _lastScanAt) >= MinScanIntervalSeconds;
    }

    private static void PerformScan()
    {
        _lastScanAt = Time.unscaledTime;
        bool hadExplicitRequest = _scanRequested;

        if (TryScanOnce(out int[] nextHandles))
        {
            SetHandles(nextHandles, hadExplicitRequest);
            _scanRequested = false;
            return;
        }

        SetHandles(Array.Empty<int>());
        _scanRequested = true;
    }

    private static bool TryScanOnce(out int[] handles)
    {
        handles = Array.Empty<int>();

        int count;
        try
        {
            count = JSL.JslConnectDevices();
        }
        catch
        {
            return false;
        }

        if (count <= 0)
            return false;

        int[] next = new int[Mathf.Max(0, count)];
        try
        {
            int copied = JSL.JslGetConnectedDeviceHandles(next, next.Length);
            copied = Mathf.Clamp(copied, 0, next.Length);
            if (copied <= 0)
                return false;

            if (copied < next.Length)
                Array.Resize(ref next, copied);
        }
        catch
        {
            return false;
        }

        handles = next;
        return true;
    }

    private static void PruneDisconnectedCachedHandles()
    {
        if (_handles.Length == 0)
            return;

        int write = 0;
        for (int i = 0; i < _handles.Length; i++)
        {
            int handle = _handles[i];
            if (!IsHandleConnected(handle))
                continue;

            _handles[write] = handle;
            write++;
        }

        if (write == _handles.Length)
            return;

        if (write <= 0)
        {
            SetHandles(Array.Empty<int>());
            _scanRequested = true;
            return;
        }

        Array.Resize(ref _handles, write);
        _revision++;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (MenuScenes.Contains(scene.name))
            RequestScan();
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added ||
            change == InputDeviceChange.Reconnected ||
            change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.Removed)
        {
            RequestScan();
        }
    }

    private static void SetHandles(int[] nextHandles, bool forceRevision = false)
    {
        if (nextHandles == null)
            nextHandles = Array.Empty<int>();

        if (AreSameHandles(_handles, nextHandles))
        {
            if (forceRevision)
                _revision++;
            return;
        }

        _handles = nextHandles;
        _revision++;
    }

    private static bool AreSameHandles(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
