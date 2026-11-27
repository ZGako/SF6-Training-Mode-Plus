using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;

namespace SF6_TMP.Modules.HitboxViewer;

/// <summary>
///     Standalone, mode-agnostic hitbox viewer that renders SF6 battle collision boxes via Dear ImGui and provides a
///     visual configuration section in REFramework's overlay menu. Can be instantiated and gated independently of
///     Training Mode (for example, in Replays or Spectate).
/// </summary>
public sealed class HitboxViewer : IDisposable
{
    private const float WorldMeterScale = 1f / 6553600f;
    private const float GameUnitScale = 1f / 65536f;
    private const float RightWallSplatX = 585.2f;
    private const float LeftWallSplatX = -RightWallSplatX;

    private const int PropPersistTotalFrames = 20;
    private const int PropPersistSlowFrames = 5;
    private const int PropPersistSharpFrames = 10;
    private const int RangeTickTotalFrames = 60;

    // Base ABGR colors (without alpha), matching ImU32 / REFramework draw colors
    private const uint ColorHitbox = 0x0040C0;
    private const uint ColorThrowbox = 0xD080FF;
    private const uint ColorClashbox = 0x3891E6;
    private const uint ColorProximity = 0x5B5B5B;
    private const uint ColorPushbox = 0x00FFFF;
    private const uint ColorHurtboxNormal = 0x00FF00;
    private const uint ColorHurtboxInvuln = 0xFF0080;
    private const uint ColorThrowHurtbox = 0xFF0000;
    private const uint ColorUniquebox = 0xEEFF00;
    private const uint ColorWhite = 0xFFFFFF;
    private const uint ColorWallSplat = 0xB729FF;
    private const uint ColorRangeTick = 0x0000FF;

    // Bits on app.PauseManager._CurrentPauseTypeBit that represent active gameplay states rather than pauses.
    private const ulong DefaultNonPauseTypeMask =
        (1UL << (int)app.PauseManager.PauseType.BATTLE) |
        (1UL << (int)app.PauseManager.PauseType.EVENT) |
        (1UL << (int)app.PauseManager.PauseType.MINIGAME_DEFAULT) |
        (1UL << (int)app.PauseManager.PauseType.WT_TRAINING_PHASE) |
        (1UL << (int)app.PauseManager.PauseType.WT_TRAINING);

    private static readonly Lock InstancesLock = new();
    private static readonly List<HitboxViewer> ActiveInstances = [];
    private static readonly string[] DisplayTargetLabels = ["Off", "P1", "P2", "P1/P2"];
    private static readonly string[] PresetLabels = ["Default", "Outline", "Custom"];

    private static readonly ImGuiRowSpec[] TableRows =
    [
        new("Hitbox Fill", true, c => c.Hitboxes, (c, v) => c.Hitboxes = v, c => c.HitboxOpacity,
            (c, v) => c.HitboxOpacity = v),
        new("Hitbox Outline", false, c => c.HitboxesOutline, (c, v) => c.HitboxesOutline = v,
            c => c.HitboxOutlineOpacity, (c, v) => c.HitboxOutlineOpacity = v),
        new("Hitbox Ticks", false, c => c.HitboxTicks, (c, v) => c.HitboxTicks = v, c => c.HitboxTickOpacity,
            (c, v) => c.HitboxTickOpacity = v),
        new("Hurtbox Fill", true, c => c.Hurtboxes, (c, v) => c.Hurtboxes = v, c => c.HurtboxOpacity,
            (c, v) => c.HurtboxOpacity = v),
        new("Hurtbox Outline", false, c => c.HurtboxesOutline, (c, v) => c.HurtboxesOutline = v,
            c => c.HurtboxOutlineOpacity, (c, v) => c.HurtboxOutlineOpacity = v),
        new("Pushbox Fill", true, c => c.Pushboxes, (c, v) => c.Pushboxes = v, c => c.PushboxOpacity,
            (c, v) => c.PushboxOpacity = v),
        new("Pushbox Outline", false, c => c.PushboxesOutline, (c, v) => c.PushboxesOutline = v,
            c => c.PushboxOutlineOpacity, (c, v) => c.PushboxOutlineOpacity = v),
        new("Throwbox Fill", true, c => c.Throwboxes, (c, v) => c.Throwboxes = v, c => c.ThrowboxOpacity,
            (c, v) => c.ThrowboxOpacity = v),
        new("Throwbox Outline", false, c => c.ThrowboxesOutline, (c, v) => c.ThrowboxesOutline = v,
            c => c.ThrowboxOutlineOpacity, (c, v) => c.ThrowboxOutlineOpacity = v),
        new("Throw Hurt Fill", true, c => c.ThrowHurtboxes, (c, v) => c.ThrowHurtboxes = v, c => c.ThrowHurtboxOpacity,
            (c, v) => c.ThrowHurtboxOpacity = v),
        new("Throw Hurt Outline", false, c => c.ThrowHurtboxesOutline, (c, v) => c.ThrowHurtboxesOutline = v,
            c => c.ThrowHurtboxOutlineOpacity, (c, v) => c.ThrowHurtboxOutlineOpacity = v),
        new("Proximity Fill", true, c => c.Proximityboxes, (c, v) => c.Proximityboxes = v, c => c.ProximityboxOpacity,
            (c, v) => c.ProximityboxOpacity = v),
        new("Proximity Outline", false, c => c.ProximityboxesOutline, (c, v) => c.ProximityboxesOutline = v,
            c => c.ProximityboxOutlineOpacity, (c, v) => c.ProximityboxOutlineOpacity = v),
        new("Clashbox Fill", true, c => c.Clashboxes, (c, v) => c.Clashboxes = v, c => c.ClashboxOpacity,
            (c, v) => c.ClashboxOpacity = v),
        new("Clashbox Outline", false, c => c.ClashboxesOutline, (c, v) => c.ClashboxesOutline = v,
            c => c.ClashboxOutlineOpacity, (c, v) => c.ClashboxOutlineOpacity = v),
        new("Uniquebox Fill", true, c => c.Uniqueboxes, (c, v) => c.Uniqueboxes = v, c => c.UniqueboxOpacity,
            (c, v) => c.UniqueboxOpacity = v),
        new("Uniquebox Outline", false, c => c.UniqueboxesOutline, (c, v) => c.UniqueboxesOutline = v,
            c => c.UniqueboxOutlineOpacity, (c, v) => c.UniqueboxOutlineOpacity = v),
        new("Properties Text", true, c => c.Properties, (c, v) => c.Properties = v, c => c.PropertiesOpacity,
            (c, v) => c.PropertiesOpacity = v),
        new("Position Marker", false, c => c.Position, (c, v) => c.Position = v, c => c.PositionOpacity,
            (c, v) => c.PositionOpacity = v)
    ];

    private static bool _isConfigBound;
    private readonly Stack<List<ScreenBox>> _boxListPool = new(8);
    private readonly List<ClassifiedBox> _classifiedBoxes = new(32);
    private readonly Dictionary<uint, List<ScreenBox>> _fillGroups = new(8);
    private readonly List<DrawCommand> _frozenDrawCommands = new(128);
    private readonly List<string> _frozenTextPool = new(32);

    private readonly List<DrawCommand> _liveDrawCommands = new(128);

    private readonly WorldToScreenProjector _projector = new();
    private readonly StringBuilder _propBuilder = new(128);

    private readonly Dictionary<(int PlayerIndex, string Text), PropPersistEntry> _propPersist = new();
    private readonly List<(int PlayerIndex, string Text)> _propPersistRemoveKeys = new(16);
    private readonly RangeTickState[] _rangeTicks = new RangeTickState[2];
    private readonly List<string> _textPool = new(32);
    private readonly List<float> _uniqueXs = new(64);
    private readonly List<float> _uniqueYs = new(64);
    private readonly List<float> _xsBuffer = new(64);
    private readonly List<float> _ysBuffer = new(64);

    private HitboxViewerConfig? _customConfig;
    private bool _disposed;
    private bool[] _gridOccupied = new bool[1024];
    private int _lastStageTimer = -1;
    private uint _propPersistEpoch;

    static HitboxViewer()
    {
        ImGuiRender.Post += OnImGuiRender;
        ImGuiDrawUI.Post += OnImGuiDrawUI;
    }

    public HitboxViewer(HitboxViewerConfig? config = null)
    {
        EnsureConfigBound();
        _customConfig = config;
        lock (InstancesLock)
        {
            ActiveInstances.Add(this);
        }
    }

    internal static ConfigSection ConfigSection => ModConfig.Instance.GetOrCreateSection(nameof(HitboxViewer));

    public static HitboxViewerConfig SharedConfig { get; private set; } = new();

    public HitboxViewerConfig Config
    {
        get => _customConfig ?? SharedConfig;
        set => _customConfig = value;
    }

    /// <summary>Gets or sets whether this viewer instance participates in rendering and overlay UI.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets whether this viewer renders its visual settings tree inside REFramework's menu.</summary>
    public bool ShowImGuiConfig { get; set; } = true;

    /// <summary>Optional host-supplied predicate evaluated each frame before rendering overlays.</summary>
    public Func<bool>? CustomRenderGate { get; set; }

    /// <summary>Bitmask of <see cref="app.PauseManager.PauseType" /> flags allowed to remain visible without hiding overlays.</summary>
    public ulong AllowedPauseTypeMask { get; set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsActive = false;

        lock (InstancesLock)
        {
            ActiveInstances.Remove(this);
        }

        ResetRuntimeState();
    }

    public static void EnsureConfigBound()
    {
        if (_isConfigBound) return;

        ConfigSection.Bind(
            () => SharedConfig,
            cfg => SharedConfig = cfg,
            HitboxViewerConfigJsonContext.Default.HitboxViewerConfig);
        _isConfigBound = true;
    }

    /// <summary>Raised whenever visual settings are modified through the ImGui configuration panel.</summary>
    public event Action? OnConfigChanged;

    public void AllowPauseType(app.PauseManager.PauseType pauseType)
    {
        var bitIndex = (int)pauseType;
        if ((uint)bitIndex < 64u)
            AllowedPauseTypeMask |= 1UL << bitIndex;
    }

    public void DisallowPauseType(app.PauseManager.PauseType pauseType)
    {
        var bitIndex = (int)pauseType;
        if ((uint)bitIndex < 64u)
            AllowedPauseTypeMask &= ~(1UL << bitIndex);
    }

    public bool IsPauseTypeAllowed(app.PauseManager.PauseType pauseType)
    {
        var bitIndex = (int)pauseType;
        return (uint)bitIndex < 64u && (AllowedPauseTypeMask & (1UL << bitIndex)) != 0;
    }

    public void ResetRuntimeState()
    {
        _liveDrawCommands.Clear();
        _frozenDrawCommands.Clear();
        _textPool.Clear();
        _frozenTextPool.Clear();
        _propPersist.Clear();
        _rangeTicks[0] = default;
        _rangeTicks[1] = default;
        _lastStageTimer = -1;
        _propPersistEpoch = 0;
    }

    internal static void ClearAllInstances()
    {
        HitboxViewer[] snapshot;
        lock (InstancesLock)
        {
            snapshot = [.. ActiveInstances];
        }

        foreach (var viewer in snapshot)
            viewer.Dispose();

        _isConfigBound = false;
    }

    private static void OnImGuiRender()
    {
        HitboxViewer[] snapshot;
        lock (InstancesLock)
        {
            if (ActiveInstances.Count == 0) return;
            snapshot = [.. ActiveInstances];
        }

        foreach (var viewer in snapshot)
            if (viewer.IsActive)
                viewer.RenderOverlay();
    }

    private static void OnImGuiDrawUI()
    {
        HitboxViewer[] snapshot;
        lock (InstancesLock)
        {
            if (ActiveInstances.Count == 0) return;
            snapshot = [.. ActiveInstances];
        }

        foreach (var viewer in snapshot)
            if (viewer.IsActive && viewer.ShowImGuiConfig)
                viewer.DrawImGuiConfigUI();
    }

    private bool IsPaused()
    {
        if (!BattleMemory.TryGetPauseState(out var pauseBits, out var isHomePausing, out var isSystemUiOverlaid))
            return false;

        if (isHomePausing || isSystemUiOverlaid)
            return true;

        var blockingMask = ~(DefaultNonPauseTypeMask | AllowedPauseTypeMask);
        return (pauseBits & blockingMask) != 0;
    }

    private int ComputeBattleFramesDelta()
    {
        if (!BattleMemory.TryGetStageTimer(out var stageTimer))
            return 1;

        if (_lastStageTimer < 0)
        {
            _lastStageTimer = stageTimer;
            return 1;
        }

        var delta = stageTimer - _lastStageTimer;
        _lastStageTimer = stageTimer;
        return delta switch
        {
            < 0 => 1,
            > 5 => 5,
            _ => delta
        };
    }

    private void RenderOverlay()
    {
        if (Config.DisplayTarget == HitboxDisplayTarget.Off) return;
        if (CustomRenderGate != null && !CustomRenderGate()) return;
        if (IsPaused()) return;

        var playersArray = BattleMemory.TryGetPlayersArray();
        if (playersArray == null || playersArray.Length < 2) return;

        if (!_projector.UpdateFrame()) return;

        var drawList = ImGui.GetBackgroundDrawList();
        if (drawList.IsNull) return;

        var battleDelta = ComputeBattleFramesDelta();

        // Preserve DR 11F timestop freeze behavior without redundant per-frame extraction
        if (BattleMemory.TryGetChronos(out var worldElapsed, out var worldNotch) &&
            worldNotch == 11 && worldElapsed > 0 && worldElapsed < worldNotch)
        {
            ReplayFrozenDrawCommands(drawList);
            UpdateAndDrawRangeTicks(drawList, battleDelta);
            return;
        }

        _liveDrawCommands.Clear();
        _textPool.Clear();
        _propPersistEpoch++;

        var worksArray = BattleMemory.TryGetGlobalWorkArray();
        if (worksArray != null)
        {
            var len = worksArray.Length;
            for (var i = 0; i < len; i++)
            {
                if (worksArray[i] is not ManagedObject workMo) continue;
                ProcessEntity(drawList, workMo, false);
            }
        }

        var playersLen = playersArray.Length;
        for (var i = 0; i < playersLen; i++)
        {
            if (playersArray[i] is not ManagedObject playerMo) continue;
            ProcessEntity(drawList, playerMo, true);
        }

        // Persist frozen snapshot by swapping buffer contents
        _frozenDrawCommands.Clear();
        _frozenDrawCommands.AddRange(_liveDrawCommands);
        _frozenTextPool.Clear();
        _frozenTextPool.AddRange(_textPool);

        DrawPersistedProperties(drawList, battleDelta);
        UpdateAndDrawRangeTicks(drawList, battleDelta);
    }

    private void ProcessEntity(ImDrawListPtr drawList, ManagedObject entityMo, bool drawPlayerMarkers)
    {
        if (!BattleMemory.TryGetEntityActParam(entityMo, out var actParamMo, out var isDead, out var playerIndex))
            return;

        if (isDead || actParamMo == null || !Config.IsPlayerVisible(playerIndex))
            return;

        var playerSettings = playerIndex == 0 ? Config.P1 : Config.P2;

        if (!BattleMemory.TryReadEntityPosition(entityMo, out var posXV, out var posYV, out var facingRight))
            return;

        if (BattleMemory.TryGetCollisionInfos(actParamMo, out var infosArray, out var count) &&
            infosArray != null && count > 0)
        {
            _classifiedBoxes.Clear();
            var trackTicks = drawPlayerMarkers && playerSettings.HitboxTicks;
            var hasFarthestStrike = false;
            var farthestEdgeX = 0f;
            var farYSum = 0f;
            var farYCount = 0;

            for (var i = count - 1; i >= 0; i--)
            {
                if (infosArray[i] is not ManagedObject rectMo) continue;
                var rectAddr = rectMo.GetAddress();
                if (rectAddr == 0 || !ManagedObject.IsManagedObject(rectAddr)) continue;

                var kind = BattleMemory.ClassifyRect(rectMo);
                if (!ClassifyBox(rectAddr, kind, playerSettings, trackTicks, out var box, out var isActiveStrike))
                    continue;

                if (!BattleMemory.TryReadRectGeometry(rectAddr, out var offX, out var offY, out var sizeX,
                        out var sizeY))
                    continue;

                var halfW = sizeX * WorldMeterScale;
                var halfH = sizeY * WorldMeterScale;
                var centerX = offX * WorldMeterScale - halfW;
                var centerY = offY * WorldMeterScale - halfH;

                if (!_projector.TryProjectWorldRect(centerX, centerY, halfW, halfH, out var sx, out var sy, out var sw,
                        out var sh))
                    continue;

                box.X = sx;
                box.Y = sy;
                box.W = sw;
                box.H = sh;
                _classifiedBoxes.Add(box);

                // Track farthest strike hitbox edge inline for range ticks (avoids a second projection pass)
                if (isActiveStrike)
                {
                    var edge = facingRight ? sx + sw : sx;
                    var midY = sy + sh * 0.5f;

                    if (hasFarthestStrike && MathF.Abs(edge - farthestEdgeX) < 0.001f)
                    {
                        farYSum += midY;
                        farYCount++;
                    }
                    else if (!hasFarthestStrike || (facingRight ? edge > farthestEdgeX : edge < farthestEdgeX))
                    {
                        hasFarthestStrike = true;
                        farthestEdgeX = edge;
                        farYSum = midY;
                        farYCount = 1;
                    }
                }
            }

            if (_classifiedBoxes.Count > 0) EmitClassifiedBoxes(drawList, playerIndex, playerSettings);

            if (trackTicks && hasFarthestStrike && farYCount > 0 && (posXV != 0 || posYV != 0) &&
                _projector.TryProjectPoint(posXV * WorldMeterScale, posYV * WorldMeterScale, 0f, out var originScreen))
                _rangeTicks[playerIndex] = new RangeTickState
                {
                    Active = true,
                    OriginX = originScreen.X,
                    FarX = farthestEdgeX,
                    FarY = farYSum / farYCount,
                    Timer = RangeTickTotalFrames
                };
        }

        if (drawPlayerMarkers && playerSettings.Position && (posXV != 0 || posYV != 0) &&
            _projector.TryProjectPoint(posXV * WorldMeterScale, posYV * WorldMeterScale, 0f, out var markerPos))
        {
            var markerColor = ColorWhite;
            if (Config.ColorWallSplat)
            {
                var scaledX = posXV * GameUnitScale;
                if ((facingRight && scaledX <= LeftWallSplatX) || (!facingRight && scaledX >= RightWallSplatX))
                    markerColor = ColorWallSplat;
            }

            var abgr = ApplyOpacity(playerSettings.PositionOpacity, markerColor);
            if (abgr != 0)
            {
                drawList.AddCircleFilled(markerPos, 10f, abgr, 12);
                _liveDrawCommands.Add(DrawCommand.Circle(markerPos.X, markerPos.Y, 10f, abgr, 12));
            }
        }
    }

    private bool ClassifyBox(
        ulong rectAddr,
        BattleRectKind kind,
        PlayerHitboxVisualSettings settings,
        bool trackTicks,
        out ClassifiedBox box,
        out bool isActiveStrike)
    {
        box = default;
        isActiveStrike = false;

        switch (kind)
        {
            case BattleRectKind.Atck:
            {
                BattleMemory.ReadAtckInfo(rectAddr, out var condFlag, out var typeFlag, out var poseBit,
                    out var guardBit);
                if (typeFlag > 0)
                {
                    isActiveStrike = trackTicks;
                    box.BaseColor = ColorHitbox;
                    box.ShowFill = settings.Hitboxes;
                    box.ShowOutline = settings.HitboxesOutline;
                    box.FillOpacity = settings.HitboxOpacity;
                    box.OutlineOpacity = settings.HitboxOutlineOpacity;
                    if (settings.Properties && (box.ShowFill || box.ShowOutline))
                        box.PropertyText = BuildHitProperties(condFlag);

                    return box.ShowFill || box.ShowOutline || isActiveStrike || box.PropertyText != null;
                }

                if ((typeFlag == 0 && poseBit > 0) || condFlag == 0x2C0)
                {
                    box.BaseColor = ColorThrowbox;
                    box.ShowFill = settings.Throwboxes;
                    box.ShowOutline = settings.ThrowboxesOutline;
                    box.FillOpacity = settings.ThrowboxOpacity;
                    box.OutlineOpacity = settings.ThrowboxOutlineOpacity;
                    if (settings.Properties && (box.ShowFill || box.ShowOutline))
                        box.PropertyText = BuildHitProperties(condFlag);

                    return box.ShowFill || box.ShowOutline || box.PropertyText != null;
                }

                if (guardBit == 0)
                {
                    box.BaseColor = ColorClashbox;
                    box.ShowFill = settings.Clashboxes;
                    box.ShowOutline = settings.ClashboxesOutline;
                    box.FillOpacity = settings.ClashboxOpacity;
                    box.OutlineOpacity = settings.ClashboxOutlineOpacity;
                }
                else
                {
                    box.BaseColor = ColorProximity;
                    box.ShowFill = settings.Proximityboxes;
                    box.ShowOutline = settings.ProximityboxesOutline;
                    box.FillOpacity = settings.ProximityboxOpacity;
                    box.OutlineOpacity = settings.ProximityboxOutlineOpacity;
                }

                return box.ShowFill || box.ShowOutline;
            }

            case BattleRectKind.Push:
                box.BaseColor = ColorPushbox;
                box.ShowFill = settings.Pushboxes;
                box.ShowOutline = settings.PushboxesOutline;
                box.FillOpacity = settings.PushboxOpacity;
                box.OutlineOpacity = settings.PushboxOutlineOpacity;
                return box.ShowFill || box.ShowOutline;

            case BattleRectKind.Damg:
            {
                BattleMemory.ReadDamgInfo(rectAddr, out var typeFlag, out var damgType, out var immune);
                if (typeFlag > 0)
                {
                    box.BaseColor = damgType is 1 or 2 ? ColorHurtboxInvuln : ColorHurtboxNormal;
                    box.ShowFill = settings.Hurtboxes;
                    box.ShowOutline = settings.HurtboxesOutline;
                    box.FillOpacity = settings.HurtboxOpacity;
                    box.OutlineOpacity = settings.HurtboxOutlineOpacity;
                    if (settings.Properties && (box.ShowFill || box.ShowOutline))
                        box.PropertyText = BuildHurtProperties(typeFlag, immune);

                    return box.ShowFill || box.ShowOutline || box.PropertyText != null;
                }

                box.BaseColor = ColorThrowHurtbox;
                box.ShowFill = settings.ThrowHurtboxes;
                box.ShowOutline = settings.ThrowHurtboxesOutline;
                box.FillOpacity = settings.ThrowHurtboxOpacity;
                box.OutlineOpacity = settings.ThrowHurtboxOutlineOpacity;
                return box.ShowFill || box.ShowOutline;
            }

            case BattleRectKind.Unique:
                box.BaseColor = ColorUniquebox;
                box.ShowFill = settings.Uniqueboxes;
                box.ShowOutline = settings.UniqueboxesOutline;
                box.FillOpacity = settings.UniqueboxOpacity;
                box.OutlineOpacity = settings.UniqueboxOutlineOpacity;
                return box.ShowFill || box.ShowOutline;

            default:
                box.BaseColor = ColorThrowHurtbox;
                box.ShowFill = settings.ThrowHurtboxes;
                box.ShowOutline = settings.ThrowHurtboxesOutline;
                box.FillOpacity = settings.ThrowHurtboxOpacity;
                box.OutlineOpacity = settings.ThrowHurtboxOutlineOpacity;
                return box.ShowFill || box.ShowOutline;
        }
    }

    private void EmitClassifiedBoxes(ImDrawListPtr drawList, int playerIndex, PlayerHitboxVisualSettings settings)
    {
        foreach (var list in _fillGroups.Values)
        {
            list.Clear();
            _boxListPool.Push(list);
        }

        _fillGroups.Clear();

        for (var i = 0; i < _classifiedBoxes.Count; i++)
        {
            var box = _classifiedBoxes[i];
            if (!box.ShowFill) continue;

            var color = ApplyOpacity(box.FillOpacity, box.BaseColor);
            if ((color & 0xFF000000u) == 0) continue;

            if (!_fillGroups.TryGetValue(color, out var list))
            {
                list = _boxListPool.Count > 0 ? _boxListPool.Pop() : new List<ScreenBox>(8);
                _fillGroups[color] = list;
            }

            list.Add(new ScreenBox(box.X, box.Y, box.W, box.H));
        }

        foreach (var (abgrColor, boxes) in _fillGroups)
            DrawUnionFills(drawList, boxes, abgrColor);

        for (var i = 0; i < _classifiedBoxes.Count; i++)
        {
            var box = _classifiedBoxes[i];
            if (!box.ShowOutline) continue;

            var abgrColor = ApplyOpacity(box.OutlineOpacity, box.BaseColor);
            if ((abgrColor & 0xFF000000u) == 0) continue;

            var pMin = new Vector2(box.X, box.Y);
            var pMax = new Vector2(box.X + box.W, box.Y + box.H);
            drawList.AddRect(pMin, pMax, abgrColor, 0f, ImDrawFlags.None, 1f);
            _liveDrawCommands.Add(DrawCommand.OutlineRect(pMin.X, pMin.Y, pMax.X, pMax.Y, abgrColor));
        }

        if (settings.Properties)
        {
            var propColor = ApplyOpacity(settings.PropertiesOpacity, ColorWhite);
            for (var i = 0; i < _classifiedBoxes.Count; i++)
            {
                var box = _classifiedBoxes[i];
                if (box.PropertyText == null) continue;

                var textIdx = _textPool.Count;
                _textPool.Add(box.PropertyText);
                drawList.AddText(new Vector2(box.X, box.Y), propColor, box.PropertyText);
                _liveDrawCommands.Add(DrawCommand.Text(box.X, box.Y, propColor, textIdx));

                var key = (playerIndex, box.PropertyText);
                if (_propPersist.TryGetValue(key, out var entry))
                {
                    entry.X = box.X;
                    entry.Y = box.Y;
                    entry.BaseOpacity = settings.PropertiesOpacity;
                    entry.Timer = PropPersistTotalFrames;
                    entry.LastLiveEpoch = _propPersistEpoch;
                    _propPersist[key] = entry;
                }
                else
                {
                    _propPersist[key] = new PropPersistEntry
                    {
                        X = box.X,
                        Y = box.Y,
                        BaseOpacity = settings.PropertiesOpacity,
                        Timer = PropPersistTotalFrames,
                        LastLiveEpoch = _propPersistEpoch
                    };
                }
            }
        }
    }

    private void DrawUnionFills(ImDrawListPtr drawList, List<ScreenBox> boxes, uint abgrColor)
    {
        if (boxes.Count == 0) return;

        if (boxes.Count == 1)
        {
            var b = boxes[0];
            var pMin = new Vector2(b.X, b.Y);
            var pMax = new Vector2(b.X + b.W, b.Y + b.H);
            drawList.AddRectFilled(pMin, pMax, abgrColor);
            _liveDrawCommands.Add(DrawCommand.FilledRect(pMin.X, pMin.Y, pMax.X, pMax.Y, abgrColor));
            return;
        }

        _xsBuffer.Clear();
        _ysBuffer.Clear();
        _uniqueXs.Clear();
        _uniqueYs.Clear();

        for (var i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i];
            _xsBuffer.Add(b.X);
            _xsBuffer.Add(b.X + b.W);
            _ysBuffer.Add(b.Y);
            _ysBuffer.Add(b.Y + b.H);
        }

        _xsBuffer.Sort();
        _ysBuffer.Sort();

        _uniqueXs.Add(_xsBuffer[0]);
        for (var i = 1; i < _xsBuffer.Count; i++)
            if (_xsBuffer[i] != _xsBuffer[i - 1])
                _uniqueXs.Add(_xsBuffer[i]);

        _uniqueYs.Add(_ysBuffer[0]);
        for (var i = 1; i < _ysBuffer.Count; i++)
            if (_ysBuffer[i] != _ysBuffer[i - 1])
                _uniqueYs.Add(_ysBuffer[i]);

        var cols = _uniqueXs.Count - 1;
        var rows = _uniqueYs.Count - 1;
        if (cols <= 0 || rows <= 0) return;

        var needed = cols * rows;
        if (_gridOccupied.Length < needed)
            _gridOccupied = new bool[needed];

        for (var r = 0; r < rows; r++)
        {
            var cy = (_uniqueYs[r] + _uniqueYs[r + 1]) * 0.5f;
            var rowOffset = r * cols;

            for (var c = 0; c < cols; c++)
            {
                var cx = (_uniqueXs[c] + _uniqueXs[c + 1]) * 0.5f;
                var covered = false;
                for (var bi = 0; bi < boxes.Count; bi++)
                {
                    var b = boxes[bi];
                    if (cx >= b.X && cx <= b.X + b.W && cy >= b.Y && cy <= b.Y + b.H)
                    {
                        covered = true;
                        break;
                    }
                }

                _gridOccupied[rowOffset + c] = covered;
            }
        }

        // Merge horizontally adjacent occupied cells per row to cut draw calls
        for (var r = 0; r < rows; r++)
        {
            var rowOffset = r * cols;
            var y0 = _uniqueYs[r];
            var y1 = _uniqueYs[r + 1];

            var c = 0;
            while (c < cols)
            {
                if (!_gridOccupied[rowOffset + c])
                {
                    c++;
                    continue;
                }

                var startC = c;
                while (c < cols && _gridOccupied[rowOffset + c])
                    c++;

                var pMin = new Vector2(_uniqueXs[startC], y0);
                var pMax = new Vector2(_uniqueXs[c], y1);
                drawList.AddRectFilled(pMin, pMax, abgrColor);
                _liveDrawCommands.Add(DrawCommand.FilledRect(pMin.X, pMin.Y, pMax.X, pMax.Y, abgrColor));
            }
        }
    }

    private void DrawPersistedProperties(ImDrawListPtr drawList, int battleDelta)
    {
        if (_propPersist.Count == 0) return;

        _propPersistRemoveKeys.Clear();
        foreach (var (key, entry) in _propPersist)
        {
            var settings = key.PlayerIndex == 0 ? Config.P1 : Config.P2;
            if (!settings.Properties || !Config.IsPlayerVisible(key.PlayerIndex))
            {
                _propPersistRemoveKeys.Add(key);
                continue;
            }

            if (entry.LastLiveEpoch < _propPersistEpoch)
            {
                var updated = entry;
                updated.Timer -= battleDelta;
                if (updated.Timer <= 0)
                {
                    _propPersistRemoveKeys.Add(key);
                }
                else
                {
                    _propPersist[key] = updated;
                    var fade = ComputePropPersistFade(updated.Timer);
                    var fadedOpacity = Math.Max(0, (int)MathF.Floor(updated.BaseOpacity * fade));
                    var color = ApplyOpacity(fadedOpacity, ColorWhite);
                    if ((color & 0xFF000000u) != 0)
                        drawList.AddText(new Vector2(updated.X, updated.Y), color, key.Text);
                }
            }
        }

        for (var i = 0; i < _propPersistRemoveKeys.Count; i++)
            _propPersist.Remove(_propPersistRemoveKeys[i]);
    }

    private void UpdateAndDrawRangeTicks(ImDrawListPtr drawList, int battleDelta)
    {
        const float tickHalfHeight = 10f;
        const float strokeThickness = 5f;

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            ref var tick = ref _rangeTicks[playerIndex];
            if (!tick.Active) continue;

            var settings = playerIndex == 0 ? Config.P1 : Config.P2;
            if (!settings.HitboxTicks || !Config.IsPlayerVisible(playerIndex) || tick.Timer <= 0)
            {
                tick.Active = false;
                continue;
            }

            var opacity = settings.HitboxTickOpacity;
            var dimFade = Math.Clamp((tick.Timer - 41f) / 19f, 0f, 1f);
            var moveFade = Math.Clamp((tick.Timer - 5f) / 20f, 0f, 1f);

            var curOx = tick.FarX - moveFade * (tick.FarX - tick.OriginX);
            var lineOpacity = (int)MathF.Floor(opacity * 0.625f * dimFade);
            var tickOpacity = (int)MathF.Floor(opacity * dimFade);

            var lineColor = ApplyOpacity(lineOpacity, ColorRangeTick);
            var tickColor = ApplyOpacity(tickOpacity, ColorRangeTick);

            var inward = tick.OriginX < tick.FarX ? -2f : 2f;
            var tickX = tick.FarX + inward;

            drawList.AddLine(new Vector2(curOx, tick.FarY), new Vector2(tickX, tick.FarY), lineColor, strokeThickness);
            drawList.AddLine(new Vector2(tickX, tick.FarY - tickHalfHeight),
                new Vector2(tickX, tick.FarY + tickHalfHeight), tickColor, strokeThickness);

            tick.Timer -= battleDelta;
            if (tick.Timer <= 0)
                tick.Active = false;
        }
    }

    private void ReplayFrozenDrawCommands(ImDrawListPtr drawList)
    {
        for (var i = 0; i < _frozenDrawCommands.Count; i++)
        {
            var cmd = _frozenDrawCommands[i];
            switch (cmd.Kind)
            {
                case DrawCommandKind.FilledRect:
                    drawList.AddRectFilled(new Vector2(cmd.X0, cmd.Y0), new Vector2(cmd.X1, cmd.Y1), cmd.Color);
                    break;
                case DrawCommandKind.OutlineRect:
                    drawList.AddRect(new Vector2(cmd.X0, cmd.Y0), new Vector2(cmd.X1, cmd.Y1), cmd.Color, 0f,
                        ImDrawFlags.None, 1f);
                    break;
                case DrawCommandKind.Circle:
                    drawList.AddCircleFilled(new Vector2(cmd.X0, cmd.Y0), cmd.X1, cmd.Color, cmd.SegmentsOrTextIdx);
                    break;
                case DrawCommandKind.Text:
                    if ((uint)cmd.SegmentsOrTextIdx < (uint)_frozenTextPool.Count)
                        drawList.AddText(new Vector2(cmd.X0, cmd.Y0), cmd.Color,
                            _frozenTextPool[cmd.SegmentsOrTextIdx]);
                    break;
            }
        }
    }

    private string? BuildHitProperties(uint condFlag)
    {
        _propBuilder.Clear();
        var anyPose = false;

        if ((condFlag & 16u) == 16u)
        {
            _propBuilder.Append("Standing, ");
            anyPose = true;
        }

        if ((condFlag & 32u) == 32u)
        {
            _propBuilder.Append("Crouching, ");
            anyPose = true;
        }

        if ((condFlag & 64u) == 64u)
        {
            _propBuilder.Append("Airborne, ");
            anyPose = true;
        }

        if ((condFlag & 256u) == 256u)
        {
            _propBuilder.Append("Can't Hit Forward, ");
            anyPose = true;
        }

        if ((condFlag & 512u) == 512u)
        {
            _propBuilder.Append("Can't Hit Backward, ");
            anyPose = true;
        }

        if (anyPose)
        {
            _propBuilder.Length -= 2;
            _propBuilder.Append('\n');
        }

        if ((condFlag & 262144u) == 262144u || (condFlag & 524288u) == 524288u)
            _propBuilder.Append("Combo Only\n");

        return _propBuilder.Length > 0 ? _propBuilder.ToString() : null;
    }

    private string? BuildHurtProperties(ushort typeFlag, byte immune)
    {
        _propBuilder.Clear();

        if (typeFlag == 1) _propBuilder.Append("Projectile Invulnerable\n");
        else if (typeFlag == 2) _propBuilder.Append("Strike Invulnerable\n");

        var intangible = false;
        if ((immune & 1) == 1)
        {
            _propBuilder.Append("Stand, ");
            intangible = true;
        }

        if ((immune & 2) == 2)
        {
            _propBuilder.Append("Crouch, ");
            intangible = true;
        }

        if ((immune & 4) == 4)
        {
            _propBuilder.Append("Air, ");
            intangible = true;
        }

        if ((immune & 64) == 64)
        {
            _propBuilder.Append("Behind, ");
            intangible = true;
        }

        if ((immune & 128) == 128)
        {
            _propBuilder.Append("Reverse, ");
            intangible = true;
        }

        if (intangible)
        {
            _propBuilder.Length -= 2;
            _propBuilder.Append(" Attack Intangible\n");
        }

        return _propBuilder.Length > 0 ? _propBuilder.ToString() : null;
    }

    private static float ComputePropPersistFade(int timer)
    {
        var age = PropPersistTotalFrames - timer;
        if (age <= PropPersistSlowFrames) return 1f;

        if (age <= PropPersistSharpFrames)
        {
            var t = (float)(age - PropPersistSlowFrames) / (PropPersistSharpFrames - PropPersistSlowFrames);
            return 1f - 0.3f * t;
        }

        var tSharp = (float)(age - PropPersistSharpFrames) / (PropPersistTotalFrames - PropPersistSharpFrames);
        return 0.7f * (1f - tSharp);
    }

    private static uint ApplyOpacity(int opacity, uint abgrBase)
    {
        var clamped = Math.Clamp(opacity, 0, 100);
        var alpha = (uint)MathF.Floor(clamped * 2.55f);
        return (alpha << 24) | (abgrBase & 0x00FFFFFFu);
    }

    private void DrawImGuiConfigUI()
    {
        if (!ImGui.TreeNode("Hitbox Viewer")) return;

        var changed = false;

        var targetIdx = (int)Config.DisplayTarget;
        ImGui.PushItemWidth(160f);
        if (ImGui.Combo("Display##hbv_display", ref targetIdx, DisplayTargetLabels, DisplayTargetLabels.Length))
        {
            Config.DisplayTarget = (HitboxDisplayTarget)targetIdx;
            changed = true;
        }

        var presetIdx = (int)Config.DetectPreset();
        if (ImGui.Combo("Preset##hbv_preset", ref presetIdx, PresetLabels, PresetLabels.Length))
        {
            var selected = (HitboxPresetKind)presetIdx;
            if (selected != HitboxPresetKind.Custom)
            {
                Config.ApplyPreset(selected);
                changed = true;
            }
        }

        ImGui.PopItemWidth();

        var syncPlayers = Config.SyncPlayers;
        if (ImGui.Checkbox("Sync P1 & P2##hbv_sync", ref syncPlayers))
        {
            Config.SyncPlayers = syncPlayers;
            if (syncPlayers) Config.P2.CopyFrom(Config.P1);
            changed = true;
        }

        ImGui.SameLine();
        var colorWallSplat = Config.ColorWallSplat;
        if (ImGui.Checkbox("Wall Splat Marker Color##hbv_splat", ref colorWallSplat))
        {
            Config.ColorWallSplat = colorWallSplat;
            changed = true;
        }

        ImGui.Spacing();

        if (ImGui.BeginTable(
                "##hbv_visual_table",
                5,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Element", ImGuiTableColumnFlags.WidthFixed, 140f);
            ImGui.TableSetupColumn("P1", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("P1 Opacity", ImGuiTableColumnFlags.WidthFixed, 105f);
            ImGui.TableSetupColumn("P2", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("P2 Opacity", ImGuiTableColumnFlags.WidthFixed, 105f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < TableRows.Length; i++)
            {
                var row = TableRows[i];
                ImGui.TableNextRow();
                ImGui.PushID(i);

                ImGui.TableSetColumnIndex(0);
                ImGui.AlignTextToFramePadding();
                ImGui.Text(row.Label);

                ImGui.TableSetColumnIndex(1);
                var p1Toggle = row.GetToggle(Config.P1);
                if (ImGui.Checkbox("##p1_t", ref p1Toggle))
                {
                    row.SetToggle(Config.P1, p1Toggle);
                    if (Config.SyncPlayers) row.SetToggle(Config.P2, p1Toggle);
                    changed = true;
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.PushItemWidth(100f);
                var p1Opa = row.GetOpacity(Config.P1);
                if (ImGui.SliderInt("##p1_o", ref p1Opa, 0, 100, "%d%%"))
                {
                    row.SetOpacity(Config.P1, p1Opa);
                    if (Config.SyncPlayers) row.SetOpacity(Config.P2, p1Opa);
                    changed = true;
                }

                ImGui.PopItemWidth();

                ImGui.TableSetColumnIndex(3);
                var p2Toggle = row.GetToggle(Config.P2);
                if (ImGui.Checkbox("##p2_t", ref p2Toggle))
                {
                    row.SetToggle(Config.P2, p2Toggle);
                    if (Config.SyncPlayers) row.SetToggle(Config.P1, p2Toggle);
                    changed = true;
                }

                ImGui.TableSetColumnIndex(4);
                ImGui.PushItemWidth(100f);
                var p2Opa = row.GetOpacity(Config.P2);
                if (ImGui.SliderInt("##p2_o", ref p2Opa, 0, 100, "%d%%"))
                {
                    row.SetOpacity(Config.P2, p2Opa);
                    if (Config.SyncPlayers) row.SetOpacity(Config.P1, p2Opa);
                    changed = true;
                }

                ImGui.PopItemWidth();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (changed)
        {
            OnConfigChanged?.Invoke();
            if (_customConfig == null) ModConfig.Save();
        }

        ImGui.TreePop();
    }

    private struct ClassifiedBox
    {
        public float X;
        public float Y;
        public float W;
        public float H;
        public uint BaseColor;
        public bool ShowFill;
        public bool ShowOutline;
        public int FillOpacity;
        public int OutlineOpacity;
        public string? PropertyText;
    }

    private readonly record struct ScreenBox(float X, float Y, float W, float H);

    private struct PropPersistEntry
    {
        public float X;
        public float Y;
        public int BaseOpacity;
        public int Timer;
        public uint LastLiveEpoch;
    }

    private struct RangeTickState
    {
        public bool Active;
        public float OriginX;
        public float FarX;
        public float FarY;
        public int Timer;
    }

    private enum DrawCommandKind : byte
    {
        FilledRect,
        OutlineRect,
        Circle,
        Text
    }

    private struct DrawCommand
    {
        public DrawCommandKind Kind;
        public float X0;
        public float Y0;
        public float X1;
        public float Y1;
        public uint Color;
        public int SegmentsOrTextIdx;

        public static DrawCommand FilledRect(float x0, float y0, float x1, float y1, uint color)
        {
            return new DrawCommand
                { Kind = DrawCommandKind.FilledRect, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Color = color };
        }

        public static DrawCommand OutlineRect(float x0, float y0, float x1, float y1, uint color)
        {
            return new DrawCommand
                { Kind = DrawCommandKind.OutlineRect, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Color = color };
        }

        public static DrawCommand Circle(float cx, float cy, float radius, uint color, int segments)
        {
            return new DrawCommand
            {
                Kind = DrawCommandKind.Circle, X0 = cx, Y0 = cy, X1 = radius, Color = color,
                SegmentsOrTextIdx = segments
            };
        }

        public static DrawCommand Text(float x, float y, uint color, int textIdx)
        {
            return new DrawCommand
                { Kind = DrawCommandKind.Text, X0 = x, Y0 = y, Color = color, SegmentsOrTextIdx = textIdx };
        }
    }

    private sealed record ImGuiRowSpec(
        string Label,
        bool _unusedGroup,
        Func<PlayerHitboxVisualSettings, bool> GetToggle,
        Action<PlayerHitboxVisualSettings, bool> SetToggle,
        Func<PlayerHitboxVisualSettings, int> GetOpacity,
        Action<PlayerHitboxVisualSettings, int> SetOpacity);
}