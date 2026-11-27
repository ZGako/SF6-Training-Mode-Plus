using System.Text.Json;
using System.Text.Json.Serialization;

namespace SF6_TMP.Modules.HitboxViewer;

/// <summary>
///     Selects which players' collision boxes and overlays are rendered by the hitbox viewer.
/// </summary>
public enum HitboxDisplayTarget
{
    Off = 0,
    P1 = 1,
    P2 = 2,
    P1AndP2 = 3
}

/// <summary>
///     Built-in visual preset configurations exposed in both the Training Pause Menu and ImGui UI.
/// </summary>
public enum HitboxPresetKind
{
    Default = 0,
    Outline = 1,
    Custom = 2
}

/// <summary>
///     Per-player visibility toggles and opacities (`0..100`) for all hitbox, hurtbox, and overlay categories.
/// </summary>
public sealed class PlayerHitboxVisualSettings
{
    public bool Hitboxes { get; set; } = true;
    public bool HitboxesOutline { get; set; } = true;
    public bool HitboxTicks { get; set; }
    public bool Hurtboxes { get; set; } = true;
    public bool HurtboxesOutline { get; set; } = true;
    public bool Pushboxes { get; set; } = true;
    public bool PushboxesOutline { get; set; } = true;
    public bool Throwboxes { get; set; } = true;
    public bool ThrowboxesOutline { get; set; } = true;
    public bool ThrowHurtboxes { get; set; } = true;
    public bool ThrowHurtboxesOutline { get; set; } = true;
    public bool Proximityboxes { get; set; } = true;
    public bool ProximityboxesOutline { get; set; } = true;
    public bool Clashboxes { get; set; } = true;
    public bool ClashboxesOutline { get; set; } = true;
    public bool Uniqueboxes { get; set; } = true;
    public bool UniqueboxesOutline { get; set; } = true;
    public bool Properties { get; set; } = true;
    public bool Position { get; set; } = true;

    public int HitboxOpacity { get; set; } = 25;
    public int HitboxOutlineOpacity { get; set; } = 25;
    public int HitboxTickOpacity { get; set; } = 25;
    public int HurtboxOpacity { get; set; } = 25;
    public int HurtboxOutlineOpacity { get; set; } = 25;
    public int PushboxOpacity { get; set; } = 25;
    public int PushboxOutlineOpacity { get; set; } = 25;
    public int ThrowboxOpacity { get; set; } = 25;
    public int ThrowboxOutlineOpacity { get; set; } = 25;
    public int ThrowHurtboxOpacity { get; set; } = 25;
    public int ThrowHurtboxOutlineOpacity { get; set; } = 25;
    public int ProximityboxOpacity { get; set; } = 25;
    public int ProximityboxOutlineOpacity { get; set; } = 25;
    public int ClashboxOpacity { get; set; } = 25;
    public int ClashboxOutlineOpacity { get; set; } = 25;
    public int UniqueboxOpacity { get; set; } = 25;
    public int UniqueboxOutlineOpacity { get; set; } = 25;
    public int PropertiesOpacity { get; set; } = 100;
    public int PositionOpacity { get; set; } = 100;

    public static PlayerHitboxVisualSettings CreateDefault()
    {
        return new PlayerHitboxVisualSettings();
    }

    public static PlayerHitboxVisualSettings CreateOutline()
    {
        return new PlayerHitboxVisualSettings
        {
            Hitboxes = false,
            HitboxesOutline = true,
            HitboxTicks = false,
            Hurtboxes = false,
            HurtboxesOutline = true,
            Pushboxes = false,
            PushboxesOutline = true,
            Throwboxes = false,
            ThrowboxesOutline = true,
            ThrowHurtboxes = false,
            ThrowHurtboxesOutline = true,
            Proximityboxes = false,
            ProximityboxesOutline = true,
            Clashboxes = false,
            ClashboxesOutline = true,
            Uniqueboxes = false,
            UniqueboxesOutline = true,
            Properties = false,
            Position = true,

            HitboxOpacity = 25,
            HitboxOutlineOpacity = 75,
            HitboxTickOpacity = 25,
            HurtboxOpacity = 25,
            HurtboxOutlineOpacity = 75,
            PushboxOpacity = 25,
            PushboxOutlineOpacity = 75,
            ThrowboxOpacity = 25,
            ThrowboxOutlineOpacity = 75,
            ThrowHurtboxOpacity = 25,
            ThrowHurtboxOutlineOpacity = 75,
            ProximityboxOpacity = 25,
            ProximityboxOutlineOpacity = 75,
            ClashboxOpacity = 25,
            ClashboxOutlineOpacity = 75,
            UniqueboxOpacity = 25,
            UniqueboxOutlineOpacity = 75,
            PropertiesOpacity = 100,
            PositionOpacity = 80
        };
    }

    public void CopyFrom(PlayerHitboxVisualSettings other)
    {
        Hitboxes = other.Hitboxes;
        HitboxesOutline = other.HitboxesOutline;
        HitboxTicks = other.HitboxTicks;
        Hurtboxes = other.Hurtboxes;
        HurtboxesOutline = other.HurtboxesOutline;
        Pushboxes = other.Pushboxes;
        PushboxesOutline = other.PushboxesOutline;
        Throwboxes = other.Throwboxes;
        ThrowboxesOutline = other.ThrowboxesOutline;
        ThrowHurtboxes = other.ThrowHurtboxes;
        ThrowHurtboxesOutline = other.ThrowHurtboxesOutline;
        Proximityboxes = other.Proximityboxes;
        ProximityboxesOutline = other.ProximityboxesOutline;
        Clashboxes = other.Clashboxes;
        ClashboxesOutline = other.ClashboxesOutline;
        Uniqueboxes = other.Uniqueboxes;
        UniqueboxesOutline = other.UniqueboxesOutline;
        Properties = other.Properties;
        Position = other.Position;

        HitboxOpacity = other.HitboxOpacity;
        HitboxOutlineOpacity = other.HitboxOutlineOpacity;
        HitboxTickOpacity = other.HitboxTickOpacity;
        HurtboxOpacity = other.HurtboxOpacity;
        HurtboxOutlineOpacity = other.HurtboxOutlineOpacity;
        PushboxOpacity = other.PushboxOpacity;
        PushboxOutlineOpacity = other.PushboxOutlineOpacity;
        ThrowboxOpacity = other.ThrowboxOpacity;
        ThrowboxOutlineOpacity = other.ThrowboxOutlineOpacity;
        ThrowHurtboxOpacity = other.ThrowHurtboxOpacity;
        ThrowHurtboxOutlineOpacity = other.ThrowHurtboxOutlineOpacity;
        ProximityboxOpacity = other.ProximityboxOpacity;
        ProximityboxOutlineOpacity = other.ProximityboxOutlineOpacity;
        ClashboxOpacity = other.ClashboxOpacity;
        ClashboxOutlineOpacity = other.ClashboxOutlineOpacity;
        UniqueboxOpacity = other.UniqueboxOpacity;
        UniqueboxOutlineOpacity = other.UniqueboxOutlineOpacity;
        PropertiesOpacity = other.PropertiesOpacity;
        PositionOpacity = other.PositionOpacity;
    }

    public bool ValueEquals(PlayerHitboxVisualSettings? other)
    {
        if (other == null) return false;
        return Hitboxes == other.Hitboxes &&
               HitboxesOutline == other.HitboxesOutline &&
               HitboxTicks == other.HitboxTicks &&
               Hurtboxes == other.Hurtboxes &&
               HurtboxesOutline == other.HurtboxesOutline &&
               Pushboxes == other.Pushboxes &&
               PushboxesOutline == other.PushboxesOutline &&
               Throwboxes == other.Throwboxes &&
               ThrowboxesOutline == other.ThrowboxesOutline &&
               ThrowHurtboxes == other.ThrowHurtboxes &&
               ThrowHurtboxesOutline == other.ThrowHurtboxesOutline &&
               Proximityboxes == other.Proximityboxes &&
               ProximityboxesOutline == other.ProximityboxesOutline &&
               Clashboxes == other.Clashboxes &&
               ClashboxesOutline == other.ClashboxesOutline &&
               Uniqueboxes == other.Uniqueboxes &&
               UniqueboxesOutline == other.UniqueboxesOutline &&
               Properties == other.Properties &&
               Position == other.Position &&
               HitboxOpacity == other.HitboxOpacity &&
               HitboxOutlineOpacity == other.HitboxOutlineOpacity &&
               HitboxTickOpacity == other.HitboxTickOpacity &&
               HurtboxOpacity == other.HurtboxOpacity &&
               HurtboxOutlineOpacity == other.HurtboxOutlineOpacity &&
               PushboxOpacity == other.PushboxOpacity &&
               PushboxOutlineOpacity == other.PushboxOutlineOpacity &&
               ThrowboxOpacity == other.ThrowboxOpacity &&
               ThrowboxOutlineOpacity == other.ThrowboxOutlineOpacity &&
               ThrowHurtboxOpacity == other.ThrowHurtboxOpacity &&
               ThrowHurtboxOutlineOpacity == other.ThrowHurtboxOutlineOpacity &&
               ProximityboxOpacity == other.ProximityboxOpacity &&
               ProximityboxOutlineOpacity == other.ProximityboxOutlineOpacity &&
               ClashboxOpacity == other.ClashboxOpacity &&
               ClashboxOutlineOpacity == other.ClashboxOutlineOpacity &&
               UniqueboxOpacity == other.UniqueboxOpacity &&
               UniqueboxOutlineOpacity == other.UniqueboxOutlineOpacity &&
               PropertiesOpacity == other.PropertiesOpacity &&
               PositionOpacity == other.PositionOpacity;
    }
}

/// <summary>
///     Visual configuration model for <see cref="HitboxViewer" />.
/// </summary>
public sealed class HitboxViewerConfig
{
    private static readonly PlayerHitboxVisualSettings DefaultTemplate = PlayerHitboxVisualSettings.CreateDefault();
    private static readonly PlayerHitboxVisualSettings OutlineTemplate = PlayerHitboxVisualSettings.CreateOutline();

    public HitboxDisplayTarget DisplayTarget { get; set; } = HitboxDisplayTarget.Off;
    public bool ColorWallSplat { get; set; } = true;
    public bool SyncPlayers { get; set; } = true;
    public PlayerHitboxVisualSettings P1 { get; set; } = PlayerHitboxVisualSettings.CreateDefault();
    public PlayerHitboxVisualSettings P2 { get; set; } = PlayerHitboxVisualSettings.CreateDefault();

    public bool IsPlayerVisible(int playerIndex)
    {
        return playerIndex switch
        {
            0 => DisplayTarget is HitboxDisplayTarget.P1 or HitboxDisplayTarget.P1AndP2,
            1 => DisplayTarget is HitboxDisplayTarget.P2 or HitboxDisplayTarget.P1AndP2,
            _ => false
        };
    }

    public void ApplyPreset(HitboxPresetKind preset)
    {
        switch (preset)
        {
            case HitboxPresetKind.Default:
                P1.CopyFrom(DefaultTemplate);
                P2.CopyFrom(DefaultTemplate);
                break;
            case HitboxPresetKind.Outline:
                P1.CopyFrom(OutlineTemplate);
                P2.CopyFrom(OutlineTemplate);
                break;
        }
    }

    public HitboxPresetKind DetectPreset()
    {
        if (P1.ValueEquals(DefaultTemplate) && P2.ValueEquals(DefaultTemplate))
            return HitboxPresetKind.Default;

        if (P1.ValueEquals(OutlineTemplate) && P2.ValueEquals(OutlineTemplate))
            return HitboxPresetKind.Outline;

        return HitboxPresetKind.Custom;
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(HitboxViewerConfig))]
internal partial class HitboxViewerConfigJsonContext : JsonSerializerContext;