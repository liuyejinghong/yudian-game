#nullable enable
using System;
using System.IO;
using Godot;
namespace Yudian;

public partial class Main
{
    private PlayerSettingsView _playerSettings = new(1, 1, "detailed", 0);
    private string PlayerSettingsPath => PrivatePlayerTest ? Path.Combine(TutorialSaveRoot, "tutorial-player-settings.cfg")
        : ProjectSettings.GlobalizePath("user://settings/tutorial-player.cfg");
    public PlayerSettingsView ReadSettings() => _playerSettings;
    public void QueueSettings(double uiScale, double cameraSpeed, string helpDetail)
        => QueuePlayer(new("settings", Vector3.Zero, -1, helpDetail, 0, null, 0, false, uiScale, cameraSpeed));
    private static void ValidateSettings(double uiScale, double cameraSpeed, string? helpDetail)
    {
        if (uiScale is not (1 or 1.25 or 1.5) || !double.IsFinite(cameraSpeed) || cameraSpeed is < .5 or > 2 || helpDetail is not ("detailed" or "brief"))
            throw new ArgumentException("设置范围无效；界面100/125/150%，镜头速度0.5–2，教学详细或精简");
    }
    private void LoadPlayerSettings()
    {
        string path = PlayerSettingsPath;
        if (!File.Exists(path)) return;
        try
        {
            if (new FileInfo(path).Length > 16_000) throw new InvalidDataException("设置文件过大");
            using var config = new ConfigFile();
            if (config.Load(path) != Error.Ok) throw new InvalidDataException("设置无法读取");
            double scale = config.GetValue("player", "ui_scale", 1.0).AsDouble();
            double speed = config.GetValue("player", "camera_speed", 1.0).AsDouble();
            string help = config.GetValue("player", "help_detail", "detailed").AsString();
            ValidateSettings(scale, speed, help); _playerSettings = new(scale, speed, help, 1);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _playerNotice = "本地设置无法读取，使用默认值；原配置保留";
            GD.Print("PLAYER_SETTINGS_REJECTED " + ex.Message);
        }
    }
    private void ProcessSettings(PlayerCommand command)
    {
        ValidateSettings(command.UiScale, command.CameraSpeed, command.Worker);
        string path = PlayerSettingsPath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var config = new ConfigFile();
            config.SetValue("player", "ui_scale", command.UiScale);
            config.SetValue("player", "camera_speed", command.CameraSpeed);
            config.SetValue("player", "help_detail", command.Worker!);
            var error = config.Save(temporary);
            if (error != Error.Ok) throw new IOException("设置写入失败：" + error);
            using (var stream = new FileStream(temporary, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
            File.Move(temporary, path, true);
            _playerSettings = new(command.UiScale, command.CameraSpeed, command.Worker!, _playerSettings.Revision + 1);
            _playerNotice = "设置已生效并保存";
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
