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

public class TaskbarApp : Form
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint VK_Q = 0x51;
    public const uint INPUT_KEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // Define other necessary PInvoke methods and structures here...

    private NotifyIcon trayIcon;
    private WaveInEvent recorder;
    private MemoryStream recordedAudio;
    private bool isRecording = false;

    public TaskbarApp()
    {
        NLog.LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(LogLevel.Info).WriteToConsole();
            builder.ForLogger().FilterMinLevel(LogLevel.Debug).WriteToFile(fileName: "file.txt");
        });
        // Initialize the tray icon and other UI elements here...
        this.trayIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            ContextMenuStrip = new ContextMenuStrip(),
            Visible = true
        };
        this.trayIcon.ContextMenuStrip.Items.Add("Configure", null, OpenConfiguration);
        this.trayIcon.ContextMenuStrip.Items.Add("Quit", null, QuitApplication);

        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;

        this.Resize += new EventHandler(Form_Resize);
        this.recordedAudio = new MemoryStream();
        // Register a global hotkey (e.g., Ctrl+Shift+Q)
        RegisterHotKey(this.Handle, 0, MOD_CONTROL | MOD_SHIFT, VK_Q);

        // Initialize the audio recorder
        recorder = new WaveInEvent();
        recorder.DataAvailable += OnDataAvailable;
        Logger.Info("init done");
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
        Form1 configForm = new Form1();
        configForm.Show();
    }

    private void QuitApplication(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Prevent the form from becoming visible
        base.SetVisibleCore(false);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            Logger.Info($"Incoming message: {m}");
            // Start or stop recording when the hotkey is pressed
            if (!isRecording)
            {
                trayIcon.ShowBalloonTip(3000, "Recording", "Recording started", ToolTipIcon.Info);
                recordedAudio = new MemoryStream();
                recorder.StartRecording();
                isRecording = true;
            }
            else
            {
                trayIcon.ShowBalloonTip(3000, "Recording", "Recording stopped", ToolTipIcon.Info);
                recorder.StopRecording();
                SendAudioToApi();
                isRecording = false;
            }
        }

        base.WndProc(ref m);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Write the recorded audio data to the memory stream
        recordedAudio.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private async void SendAudioToApi()
    {
        try
        {
            // Convert the recorded audio to the format required by the API
            byte[] audioData = recordedAudio.ToArray();
            using MemoryStream wavStream = new MemoryStream();
            using (var writer = new WaveFileWriter(wavStream, recorder.WaveFormat))
            {
                writer.Write(audioData, 0, audioData.Length);
            }

            // Create a new MemoryStream for the API request
            // This is necessary because the WaveFileWriter closes the original MemoryStream
            using MemoryStream apiStream = new MemoryStream(wavStream.ToArray());

            // Create an OpenAIClient
            OpenAIClient client = new OpenAIClient(ConfigurationManager.ReadApiKey());

            // Create an AudioTranscriptionRequest
            var request = new AudioTranscriptionRequest(apiStream, null, language: "en");

            // Send the request and get the response
            var audioResult = await client.AudioEndpoint.CreateTranscriptionAsync(request);
            if (audioResult != null)
            {
                Logger.Info($"Result: {audioResult}");

                // Simulate typing the text
                var sim = new InputSimulator();
                foreach (char c in audioResult)
                {
                    // Use InputSimulator to simulate key press
                    sim.Keyboard.TextEntry(c.ToString());
                    Thread.Sleep(10); // optional delay between each character
                }
            }
            else
            {
                Logger.Info("No transcription available.");
            }
        }
        catch (Exception ex)
        {
            // Handle any exceptions
            Logger.Error($"Error sending audio to API: {ex.Message}");
        }
    }

    public static void Main()
    {
        Application.Run(new TaskbarApp());
    }
}