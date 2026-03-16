using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ControllerManagerScript : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text xboxConnectedText;
    [SerializeField] private TMP_Text joyConConnectedText;

    [Header("Labels")]
    [SerializeField] private string connectedLabel = "Connected!";
    [SerializeField] private string disconnectedLabel = "Not Connected";
    [SerializeField] private Color connectedColor = new Color(0f, 0.7f, 0f, 1f);
    [SerializeField] private Color disconnectedColor = new Color(0.75f, 0f, 0f, 1f);

    [Header("Detection")]
    [SerializeField] private bool enableControllerDetection = true;
    [SerializeField] private float refreshInterval = 0.1f;
    [SerializeField] private bool logDetection = false;
    [SerializeField] private bool preferWirelessGamepadAsJoyCon = true;

    private float _nextRefreshTime;
    private bool _lastXboxConnected;
    private bool _lastJoyConConnected;
    private string _lastSnapshot = string.Empty;

    private void Awake()
    {
        if (!enableControllerDetection)
            return;

        AutoBindUiIfNeeded();
    }

    private void OnEnable()
    {
        if (!enableControllerDetection)
            return;

        AutoBindUiIfNeeded();
        RefreshControllerStatus(forceLog: false);
    }

    private void Update()
    {
        if (!enableControllerDetection)
            return;

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        RefreshControllerStatus(forceLog: false);
    }

    private void AutoBindUiIfNeeded()
    {
        if (xboxConnectedText == null)
        {
            xboxConnectedText = FindTextByName(transform, "XboxConnected");
            if (xboxConnectedText == null)
            {
                GameObject xboxGo = GameObject.Find("XboxConnected");
                if (xboxGo != null)
                    xboxConnectedText = xboxGo.GetComponent<TMP_Text>();
            }
        }

        if (joyConConnectedText == null)
        {
            joyConConnectedText = FindTextByName(transform, "JoyConConnected");
            if (joyConConnectedText == null)
            {
                GameObject joyConGo = GameObject.Find("JoyConConnected");
                if (joyConGo != null)
                    joyConConnectedText = joyConGo.GetComponent<TMP_Text>();
            }
        }
    }

    public void RefreshNow()
    {
        if (!enableControllerDetection)
            return;

        AutoBindUiIfNeeded();
        RefreshControllerStatus(forceLog: false);
    }

    private void RefreshControllerStatus(bool forceLog)
    {
        DetectControllerStatus(out bool xboxConnected, out bool joyConConnected, out string snapshot);

        SetStatusText(xboxConnectedText, xboxConnected);
        SetStatusText(joyConConnectedText, joyConConnected);

        bool changed =
            forceLog ||
            xboxConnected != _lastXboxConnected ||
            joyConConnected != _lastJoyConConnected ||
            !string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal);

        if (logDetection && changed)
        {
            Debug.Log($"ControllerMenu status -> Xbox:{xboxConnected}, JoyCon:{joyConConnected} | {snapshot}");
        }

        _lastXboxConnected = xboxConnected;
        _lastJoyConConnected = joyConConnected;
        _lastSnapshot = snapshot;
    }

    private void DetectControllerStatus(out bool xboxConnected, out bool joyConConnected, out string snapshot)
    {
        xboxConnected = false;
        joyConConnected = false;
        bool sawAmbiguousWirelessGamepad = false;

        string[] joystickNames = GetConnectedJoystickNames();
        bool hasNamedController = false;

        for (int i = 0; i < joystickNames.Length; i++)
        {
            string name = joystickNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            hasNamedController = true;
            string lower = name.ToLowerInvariant();

            bool isJoyConLike =
                lower.Contains("joy-con") ||
                lower.Contains("joycon") ||
                lower.Contains("nintendo") ||
                lower.Contains("switch") ||
                lower.Contains("pro controller");

            bool isXboxLike =
                lower.Contains("xbox") ||
                lower.Contains("xinput") ||
                lower.Contains("microsoft");

            bool isAmbiguousWirelessGamepad = lower.Contains("wireless gamepad");

            if (isJoyConLike)
                joyConConnected = true;
            else if (isXboxLike)
                xboxConnected = true;
            else if (isAmbiguousWirelessGamepad)
                sawAmbiguousWirelessGamepad = true;
        }

        // Fallback only when legacy joystick names provide no connected devices.
        if (!hasNamedController)
        {
            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                InputDevice device = InputSystem.devices[i];
                if (device == null || device is not Gamepad)
                    continue;

                string descriptor = BuildDescriptor(device);
                bool isJoyConLike =
                    descriptor.Contains("joy-con") ||
                    descriptor.Contains("joycon") ||
                    descriptor.Contains("nintendo") ||
                    descriptor.Contains("switch") ||
                    descriptor.Contains("pro controller");

                bool isXboxLike =
                    descriptor.Contains("xbox") ||
                    descriptor.Contains("xinput") ||
                    descriptor.Contains("microsoft");

                bool isAmbiguousWirelessGamepad = descriptor.Contains("wireless gamepad");

                if (isJoyConLike)
                    joyConConnected = true;
                else if (isAmbiguousWirelessGamepad)
                    sawAmbiguousWirelessGamepad = true;
                else if (isXboxLike)
                    xboxConnected = true;
            }
        }

        // If Unity only reports "Wireless Gamepad", prefer a configurable default.
        if (sawAmbiguousWirelessGamepad)
        {
            if (preferWirelessGamepadAsJoyCon && !joyConConnected)
                joyConConnected = true;
            else if (!preferWirelessGamepadAsJoyCon && !xboxConnected)
                xboxConnected = true;
        }

        snapshot = BuildSnapshot(joystickNames);
    }

    private static string[] GetConnectedJoystickNames()
    {
        try
        {
            string[] names = Input.GetJoystickNames();
            return names ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildDescriptor(InputDevice device)
    {
        string display = device.displayName ?? string.Empty;
        string product = device.description.product ?? string.Empty;
        string manufacturer = device.description.manufacturer ?? string.Empty;
        string interfaceName = device.description.interfaceName ?? string.Empty;
        return (display + " | " + product + " | " + manufacturer + " | " + interfaceName).ToLowerInvariant();
    }

    private static string BuildSnapshot(string[] names)
    {
        if (names == null || names.Length == 0)
            return "joysticks: []";

        return "joysticks: [" + string.Join(", ", names) + "]";
    }

    private void SetStatusText(TMP_Text text, bool connected)
    {
        if (text == null)
            return;

        text.text = connected ? connectedLabel : disconnectedLabel;
        text.color = connected ? connectedColor : disconnectedColor;
    }

    private static TMP_Text FindTextByName(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name && child.TryGetComponent(out TMP_Text text))
                return text;

            TMP_Text nested = FindTextByName(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
