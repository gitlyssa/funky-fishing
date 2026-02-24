using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class ControllerManagerScript : MonoBehaviour
{
    [Header("Optional UI References (auto-found by name if left empty)")]
    [SerializeField] private TMP_Text xboxConnectedText;
    [SerializeField] private TMP_Text joyConConnectedText;

    [Header("Status Labels")]
    [SerializeField] private string connectedLabel = "Connected!";
    [SerializeField] private string disconnectedLabel = "Not Connected";
    [SerializeField] private Color connectedColor = new Color(0f, 0.7f, 0f, 1f);
    [SerializeField] private Color disconnectedColor = new Color(0.75f, 0f, 0f, 1f);

    [Header("Detection")]
    [SerializeField] private bool treatAnyGamepadAsXbox = true;
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private bool treatNintendoGamepadAsJoyCon = true;
    [SerializeField] private bool shutdownJoyShockInMenu = true;

    private float _nextRefreshTime;

    private void OnEnable()
    {
        if (shutdownJoyShockInMenu)
            SafeShutdownJoyShock();

        AutoBindUiIfNeeded();
        RefreshControllerStatus();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        RefreshControllerStatus();
    }

    private void AutoBindUiIfNeeded()
    {
        if (xboxConnectedText == null)
        {
            GameObject xboxGo = GameObject.Find("XboxConnected");
            if (xboxGo != null)
                xboxConnectedText = xboxGo.GetComponent<TMP_Text>();
        }

        if (joyConConnectedText == null)
        {
            GameObject joyConGo = GameObject.Find("JoyConConnected");
            if (joyConGo != null)
                joyConConnectedText = joyConGo.GetComponent<TMP_Text>();
        }
    }

    private void RefreshControllerStatus()
    {
        bool xboxConnected = IsXboxConnected();
        bool joyConConnected = IsJoyConConnected();

        SetStatusText(xboxConnectedText, xboxConnected);
        SetStatusText(joyConConnectedText, joyConConnected);
    }

    private void SetStatusText(TMP_Text text, bool connected)
    {
        if (text == null)
            return;

        text.text = connected ? connectedLabel : disconnectedLabel;
        text.color = connected ? connectedColor : disconnectedColor;
    }

    private bool IsXboxConnected()
    {
        string[] names = GetConnectedJoystickNames();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string lower = name.ToLowerInvariant();
            bool looksLikeJoyCon =
                lower.Contains("joy-con") ||
                lower.Contains("joycon") ||
                lower.Contains("nintendo");

            if (looksLikeJoyCon)
                continue;

            if (treatAnyGamepadAsXbox)
                return true;

            if (lower.Contains("xbox") ||
                lower.Contains("xinput") ||
                lower.Contains("microsoft"))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsJoyConConnected()
    {
        string[] names = GetConnectedJoystickNames();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string lower = name.ToLowerInvariant();

            bool looksLikeJoyCon =
                lower.Contains("joy-con") ||
                lower.Contains("joycon");

            bool looksLikeNintendoGamepad =
                treatNintendoGamepadAsJoyCon &&
                (lower.Contains("nintendo") ||
                 lower.Contains("pro controller"));

            if (looksLikeJoyCon || looksLikeNintendoGamepad)
                return true;
        }

        return false;
    }

    private static string[] GetConnectedJoystickNames()
    {
        try
        {
            string[] names = Input.GetJoystickNames();
            return names ?? Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private static void SafeShutdownJoyShock()
    {
        try
        {
            JSL.JslDisconnectAndDisposeAll();
        }
        catch (DllNotFoundException)
        {
            // JoyShockLibrary not present on this platform/build.
        }
        catch (EntryPointNotFoundException)
        {
            // Unexpected JoyShockLibrary mismatch; ignore in menu.
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ControllerManagerScript: JoyShock shutdown failed safely: {ex.Message}");
        }
    }

    public void BackToMenu()
    {
        Debug.Log("Back to Menu");
        SceneManager.LoadScene("MainMenu");
    }
}
