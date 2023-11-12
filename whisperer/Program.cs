using System;
using System.Windows.Forms;
using NAudio.Wave;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text;
using System.IO;
using OpenAI;
using NLog;
using whisperer;
using OpenAI.Audio;
using WindowsInput;
using Microsoft.VisualBasic.Logging;
using static System.Windows.Forms.DataFormats;

namespace whisperer;
public class TaskbarApp : Form
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint VK_Q = 0x51;
    private const uint INPUT_KEYBOARD = 1;
    private NotifyIcon trayIcon;
    private WaveInEvent recorder;
    private MemoryStream recordedAudio;
    private bool isRecording = false;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public TaskbarApp()
    {
        SetupLogger();
        InitializeUI();
        RegisterHotKey();

        InitializeRecorder();
        ConfigurationManager.SettingsSaved += (sender, e) => ReloadSettings();
        Logger.Info("init done");
    }

    private void RegisterHotKey()
    {
        Keys hotkey = ConfigurationManager.ReadHotkey();

        uint hotkeyModifier = 0;

        if ((hotkey & Keys.Control) == Keys.Control)
        {
            hotkeyModifier |= MOD_CONTROL;
        }

        if ((hotkey & Keys.Shift) == Keys.Shift)
        {
            hotkeyModifier |= MOD_SHIFT;
        }

        if ((hotkey & Keys.Alt) == Keys.Alt)
        {
            hotkeyModifier |= MOD_ALT;
        }

        if ((hotkey & Keys.LWin) == Keys.LWin || (hotkey & Keys.RWin) == Keys.RWin)
        {
            hotkeyModifier |= MOD_WIN;
        }

        uint hotkeyKey = (uint)hotkey & 0xFFFF; // This gets the key code of the hotkey

        RegisterHotKey(this.Handle, 0, hotkeyModifier, hotkeyKey);
    }

    public void UnregisterHotKey()
    {
        UnregisterHotKey(this.Handle, 0);
    }

    private void ReloadSettings()
    {
        Logger.Info("triggered config reload");
        UnregisterHotKey();
        RegisterHotKey();
    }


    private void SetupLogger()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string logFilePath = Path.Combine(appDataPath, "whisperer", "logs", "whisperer.log");

        // Ensure the directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));

        var config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        var logFile = new NLog.Targets.FileTarget("logfile")
        {
            FileName = logFilePath,
            ArchiveAboveSize = 1048576, // 1MB in bytes
            ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling,
            MaxArchiveFiles = 5,
            ConcurrentWrites = true,
            KeepFileOpen = false,
        };
        var logConsole = new NLog.Targets.ConsoleTarget("logconsole");

        // Rules for mapping loggers to targets            
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, logFile);

        // Apply config           
        NLog.LogManager.Configuration = config;
    }

    private void InitializeUI()
    {
        this.trayIcon = new NotifyIcon()
        {
            Icon = new Icon("whisperer.ico"),
            ContextMenuStrip = new ContextMenuStrip(),
            Visible = true
        };
        this.trayIcon.ContextMenuStrip.Items.Add("Configure", null, OpenConfiguration);
        this.trayIcon.ContextMenuStrip.Items.Add("Quit", null, QuitApplication);
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Resize += new EventHandler(Form_Resize);
        this.recordedAudio = new MemoryStream();
    }

    private void InitializeRecorder()
    {
        recorder = new WaveInEvent();
        recorder.DataAvailable += OnDataAvailable;
    }

    private void Form_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            this.Hide();
        }
    }

    private void OpenConfiguration(object? sender, EventArgs e)
    {
        ConfigForm configForm = new ConfigForm();
        configForm.Show();
    }

    private void QuitApplication(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            HandleHotkeyPress();
        }
        base.WndProc(ref m);
    }

    private void HandleHotkeyPress()
    {
        Logger.Info($"Hotkey pressed");
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    private void StartRecording()
    {
        trayIcon.ShowBalloonTip(3000, "Recording", "Recording started", ToolTipIcon.Info);
        recordedAudio = new MemoryStream();
        recorder.StartRecording();
        isRecording = true;
    }

    private void StopRecording()
    {
        trayIcon.ShowBalloonTip(3000, "Recording", "Recording stopped", ToolTipIcon.Info);
        recorder.StopRecording();
        SendAudioToApi();
        isRecording = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        recordedAudio.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private async void SendAudioToApi()
    {
        try
        {
            byte[] audioData = ConvertAudioToApiFormat();
            var audioResult = await TranscribeAudio(audioData);
            if (audioResult != null)
            {
                SimulateTyping(audioResult);
            }
            else
            {
                Logger.Info("No transcription available.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error sending audio to API: {ex.Message}");
        }
    }

    private byte[] ConvertAudioToApiFormat()
    {
        byte[] audioData = recordedAudio.ToArray();
        using MemoryStream wavStream = new MemoryStream();
        using (var writer = new WaveFileWriter(wavStream, recorder.WaveFormat))
        {
            writer.Write(audioData, 0, audioData.Length);
        }
        return wavStream.ToArray();
    }

    private async Task<string> TranscribeAudio(byte[] audioData)
    {
        using MemoryStream apiStream = new MemoryStream(audioData);
        OpenAIClient client = new OpenAIClient(ConfigurationManager.ReadApiKey());
        var request = new AudioTranscriptionRequest(apiStream, null, language: "en");
        return await client.AudioEndpoint.CreateTranscriptionAsync(request);
    }

    private void SimulateTyping(string text)
    {
        Logger.Info($"Result: {text}");
        var sim = new InputSimulator();
        foreach (char c in text)
        {
            sim.Keyboard.TextEntry(c.ToString());
            Thread.Sleep(10);
        }
    }

    public static void Main()
    {
        Application.Run(new TaskbarApp());
    }
}
