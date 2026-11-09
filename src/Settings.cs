using Godot;

namespace ChopChopMeow;

/// <summary>
/// How the world's edges are smoothed: not at all, by FSR 2 upscaling a smaller render, by FSR 2 at full size, or by TAA at full size.
/// The numbers are what <c>settings.cfg</c> stores.
/// </summary>
public enum AntiAliasing { Off = 0, FsrUpscale = 1, Taa = 2, FsrNative = 3 }

/// <summary>The player's settings file.</summary>
public static class Settings {
    public static string Path => $"user://{Launch.FilePrefix}settings.cfg";

    /// <summary>The size the project is laid out for; the interface is drawn larger at a taller render size.</summary>
    public static readonly Vector2I DesignSize = new(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(), ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32());

    /// <summary>The stored render size, or the project's design size before the player picks one.</summary>
    public static Vector2I Resolution => Get("display", "resolution", DesignSize);

    public static int FrameLimit => Get("display", "frame_limit", 0);
    /// <summary>The stored anti-aliasing choice, or FSR Native before the player picks one or when the file holds something else.</summary>
    public static AntiAliasing AntiAliasing => Get("display", "anti_aliasing", (int)AntiAliasing.FsrNative) is var stored && System.Enum.IsDefined((AntiAliasing)stored) ? (AntiAliasing)stored : AntiAliasing.FsrNative;
    public static bool Fullscreen => Get("display", "fullscreen", false);
    public static bool FrameRate => Get("display", "fps", true);

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

    /// <summary>FSR 2 renders the world at this share of the render size and upscales it, as AMD's Quality preset does.</summary>
    public const float FsrScale = .67f;

    /// <summary>The size FSR Upscale draws the world at for a render size, rounded down as Godot does.</summary>
    public static Vector2I FsrUpscaleSize(Vector2I render) => (Vector2I)((Vector2)render * FsrScale);

    /// <summary>Smooths the world's edges in <paramref name="viewport"/> the chosen way; the interface always draws at full size.</summary>
    public static void ApplyAntiAliasing(Viewport viewport, AntiAliasing choice) {
        viewport.UseTaa = choice == AntiAliasing.Taa;
        viewport.Scaling3DMode = choice is AntiAliasing.FsrUpscale or AntiAliasing.FsrNative ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = choice == AntiAliasing.FsrUpscale ? FsrScale : 1;
    }

    public static T Get<[MustBeVariant] T>(string section, string key, T fallback) {
        var config = new ConfigFile(); config.Load(Path);
        return config.GetValue(section, key, Variant.From(fallback)).As<T>();
    }

    public static void Store<[MustBeVariant] T>(string section, string key, T value) {
        var config = new ConfigFile(); config.Load(Path);
        config.SetValue(section, key, Variant.From(value)); config.Save(Path);
    }

    /// <summary>
    /// Renders at the chosen size: the window shows that image scaled to fit, with bars when its shape differs.
    /// Fullscreen covers the whole screen with no frame.
    /// Otherwise the window opens at the render size, or as large as the screen allows at that shape; resizing it after only stretches the image.
    /// </summary>
    public static void ApplyResolution(Window window, Vector2I size, bool fullscreen) {
        window.ContentScaleSize = size;
        if (fullscreen) { window.Mode = Window.ModeEnum.Fullscreen; return; }
        var room = (Vector2)DisplayServer.ScreenGetUsableRect().Size;
        float fit = Mathf.Min(1, Mathf.Min(room.X / size.X, room.Y / size.Y));
        window.Mode = Window.ModeEnum.Windowed; window.Size = (Vector2I)((Vector2)size * fit); window.MoveToCenter();
    }
}
