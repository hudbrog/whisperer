using System;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.ComponentModel;

namespace whisperer;
public partial class Form1 : Form
{
    private TextBox apiKeyTextBox;
    private TextBox hotkeyTextBox;
    private Keys currentHotkey;
    private bool waitingForHotkey = false;
    private Button saveButton;
    private Label apiKeyLabel;
    private Label hotkeyLabel;
    private Label copyrightLabel;
    private TableLayoutPanel tableLayoutPanel;

    public Form1()
    {
        InitializeComponent();
        InitializeFormControls();
        LoadApiKey();
        InitializeHotkeyControl();
        this.Size = new Size(400, 200); // Set a proper size for the form
        LoadHotkey();

        // apiKeyTextBox = new TextBox()!;
        // hotkeyTextBox = new TextBox()!;
        // saveButton = new Button()!;
        // apiKeyLabel = new Label()!;
        // hotkeyLabel = new Label()!;
        // tableLayoutPanel = new TableLayoutPanel()!;
        // copyrightLabel = new Label()!;
    }

    private void InitializeFormControls()
    {
        tableLayoutPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Fill
        };
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.Controls.Add(tableLayoutPanel);

        // API Key controls
        apiKeyLabel = new Label { Text = "API Key:" };
        tableLayoutPanel.Controls.Add(apiKeyLabel, 0, 0);
        apiKeyTextBox = new TextBox
        {
            Width = 300
        };
        tableLayoutPanel.Controls.Add(apiKeyTextBox, 1, 0);

        // Hotkey controls
        hotkeyLabel = new Label { Text = "Hotkey:" };
        tableLayoutPanel.Controls.Add(hotkeyLabel, 0, 1);

        // Save Button
        saveButton = new Button { Text = "Save" };
        saveButton.Click += saveButton_Click;
        tableLayoutPanel.Controls.Add(saveButton, 1, 2);

        // Copyright Information
        copyrightLabel = new Label
        {
            Text = "Whisperer by hudbrog\n" +
                   "For more information visit [GitHub Placeholder]\n" +
                   "© 2023 hudbrog",
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(copyrightLabel);
    }

    private void LoadApiKey()
    {
        apiKeyTextBox.Text = ConfigurationManager.ReadApiKey();
    }

    private void saveButton_Click(object? sender, EventArgs e)
    {
        ConfigurationManager.SaveApiKey(apiKeyTextBox.Text);
        ConfigurationManager.SaveHotkey(currentHotkey);
        MessageBox.Show("Settings saved securely.");
    }

    private void InitializeHotkeyControl()
    {
        hotkeyTextBox = new TextBox
        {
            ReadOnly = true, // Prevent typing in the control
            Cursor = Cursors.Hand, // Indicate that the control can be clicked
            Width = 300
        };
        hotkeyTextBox.Click += HotkeyTextBox_Click;
        hotkeyTextBox.KeyDown += HotkeyTextBox_KeyDown;
        hotkeyTextBox.KeyUp += HotkeyTextBox_KeyUp;
        hotkeyTextBox.LostFocus += HotkeyTextBox_LostFocus;
        tableLayoutPanel.Controls.Add(hotkeyTextBox, 1, 1);
    }

    private void HotkeyTextBox_Click(object? sender, EventArgs e)
    {
        // When the user clicks the control, start waiting for a hotkey press
        waitingForHotkey = true;
        hotkeyTextBox.Text = "Press a hotkey...";
    }

    private void HotkeyTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (waitingForHotkey)
        {
            // Update the text box with the pressed key combination
            currentHotkey = e.KeyCode | e.Modifiers;
            hotkeyTextBox.Text = TypeDescriptor.GetConverter(typeof(Keys)).ConvertToString(currentHotkey);
            e.SuppressKeyPress = true;  // Prevent further processing of the key
        }
    }

    private void HotkeyTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (waitingForHotkey)
        {
            waitingForHotkey = false;
        }
    }

    private void HotkeyTextBox_LostFocus(object? sender, EventArgs e)
    {
        if (waitingForHotkey)
        {
            // Reset if the user clicks away without pressing a key
            hotkeyTextBox.Text = string.Empty;
            currentHotkey = Keys.None;
            waitingForHotkey = false;
        }
    }
    private void LoadHotkey()
    {
        currentHotkey = ConfigurationManager.ReadHotkey();
        hotkeyTextBox.Text = TypeDescriptor.GetConverter(typeof(Keys)).ConvertToString(currentHotkey);
    }

}
