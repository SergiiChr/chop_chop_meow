using Godot;

namespace ChopChopMeow;

/// <summary>The player's settings file.</summary>
public static class Settings {
    public static string Path => $"user://{Launch.FilePrefix}settings.cfg";

    /// <summary>The stored window size, or the project's default size before the player picks one.</summary>
    public static Vector2I Resolution => Get("display", "resolution",
        new Vector2I(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(), ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32()));

    public static int FrameLimit => Get("display", "frame_limit", 0);
    public static bool Taa => Get("display", "taa", true);

    public static float MusicVolume => Get("audio", "music", 1f);

    /// <summary>
    /// The key this machine joins games with, made once; each world saves this machine's player under it.
    /// The network check's host and guest share one settings file, so each adds its role to tell them apart.
    /// </summary>
    public static string MachineKey {
        get {
            string key = Get("net", "key", "");
            if (key == "") Store("net", "key", key = System.Guid.NewGuid().ToString("N"));
            return Launch.Value("--net-check") is string role ? $"{key}-{role}" : key;
        }
    }

    public static T Get<[MustBeVariant] T>(string section, string key, T fallback) {
        var config = new ConfigFile(); config.Load(Path);
        return config.GetValue(section, key, Variant.From(fallback)).As<T>();
    }

    public static void Store<[MustBeVariant] T>(string section, string key, T value) {
        var config = new ConfigFile(); config.Load(Path);
        config.SetValue(section, key, Variant.From(value)); config.Save(Path);
    }

    /// <summary>Sizes the game window and centres it.</summary>
    public static void ApplyResolution(Window window, Vector2I size) {
        window.Mode = Window.ModeEnum.Windowed; window.Size = size; window.MoveToCenter();
    }
}
