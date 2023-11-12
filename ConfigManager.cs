using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

public class ConfigurationManager
{
    public delegate void SettingsSavedEventHandler(object sender, EventArgs e);
    public static event SettingsSavedEventHandler SettingsSaved;


    private static readonly string AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string ApiKeyFileName = Path.Combine(AppDataFolder, "apikey.dat");
    private static readonly string HotkeyFileName = Path.Combine(AppDataFolder, "hotkey.dat");

    public static string ReadApiKey()
    {
        try
        {
            if (File.Exists(ApiKeyFileName))
            {
                byte[] encryptedApiKey = File.ReadAllBytes(ApiKeyFileName);
                byte[] apiKeyBytes = ProtectedData.Unprotect(encryptedApiKey, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(apiKeyBytes);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading API key: {ex.Message}");
        }

        return string.Empty;
    }

    public static Keys ReadHotkey()
    {
        Keys defaultHotkey = Keys.Control | Keys.Shift | Keys.W;
        try
        {
            if (File.Exists(HotkeyFileName))
            {
                byte[] encryptedHotkey = File.ReadAllBytes(HotkeyFileName);
                byte[] hotkeyBytes = ProtectedData.Unprotect(encryptedHotkey, null, DataProtectionScope.CurrentUser);
                return (Keys)BitConverter.ToInt32(hotkeyBytes, 0);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading hotkey: {ex.Message}");
        }

        return defaultHotkey;
    }

    public static void SaveApiKey(string apiKey)
    {
        try
        {
            byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] encryptedApiKey = ProtectedData.Protect(apiKeyBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(ApiKeyFileName, encryptedApiKey);
            SettingsSaved?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving API key: {ex.Message}");
        }
    }

    public static void SaveHotkey(Keys hotkey)
    {
        try
        {
            byte[] hotkeyBytes = BitConverter.GetBytes((int)hotkey);
            byte[] encryptedHotkey = ProtectedData.Protect(hotkeyBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(HotkeyFileName, encryptedHotkey);
            SettingsSaved?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving hotkey: {ex.Message}");
        }
    }
}
