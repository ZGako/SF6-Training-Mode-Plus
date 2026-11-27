namespace SF6_TMP.Modules.HitboxViewer;

internal enum BattleRectKind : byte
{
    Unknown = 0,
    Atck = 1,
    Damg = 2,
    Push = 3,
    Unique = 4
}

/// <summary>
///     Zero-proxy battle memory reader backed by statically cached TDB handles. Designed for 60 FPS reads without
///     triggering REFramework.NET <c>ProxyBuilder</c> generation on <c>nBattle</c>/<c>nAction</c> types.
/// </summary>
internal static class BattleMemory
{
    private static readonly TDB Tdb = API.GetTDB();

    // gBattle statics
    private static readonly TypeDefinition? BattleType = Tdb.FindType("gBattle");
    private static readonly Field? BattlePlayerField = BattleType?.GetField("Player");
    private static readonly Field? BattleWorkField = BattleType?.GetField("Work");
    private static readonly Field? BattleChronosField = BattleType?.GetField("Chronos");
    private static readonly Field? BattleGameField = BattleType?.GetField("Game");

    // Singletons / subsystem containers
    private static readonly Field? SPlayerMcPlayerField = nBattle.sPlayer.REFType?.GetField("mcPlayer");
    private static readonly Field? SWorkGlobalWorkField = nBattle.sWork.REFType?.GetField("Global_work");
    private static readonly Field? SGameStageTimerField = nBattle.sGame.REFType?.GetField("stage_timer");

    // Chronos fields
    private static readonly TypeDefinition? ChronosType = Tdb.FindType("app.BattleChronos");
    private static readonly Field? ChronosWorldElapsedField = ChronosType?.GetField("WorldElapsed");
    private static readonly Field? ChronosWorldNotchField = ChronosType?.GetField("WorldNotch");

    // PauseManager fields
    private static readonly TypeDefinition? PauseManagerType = Tdb.FindType("app.PauseManager");
    private static readonly Field? CurrentPauseTypeBitField = PauseManagerType?.GetField("_CurrentPauseTypeBit");
    private static readonly Field? IsHomePausingField = PauseManagerType?.GetField("IsHomePausing");
    private static readonly Field? IsSystemUiOverlaidField = PauseManagerType?.GetField("IsSystemUiOverlaid");

    // cWork fields and methods
    private static readonly TypeDefinition? CWorkType = Tdb.FindType("nBattle.cWork");
    private static readonly Field? WorkActParamField = CWorkType?.GetField("mpActParam");
    private static readonly Field? WorkPosField = CWorkType?.GetField("pos");
    private static readonly Field? WorkBitValueField = CWorkType?.GetField("BitValue");
    private static readonly Method? WorkIsR0DieMethod = CWorkType?.GetMethod("get_IsR0Die");
    private static readonly Method? WorkIsTeam1PMethod = CWorkType?.GetMethod("get_IsTeam1P");
    private static readonly Method? WorkIsTeam2PMethod = CWorkType?.GetMethod("get_IsTeam2P");

    // via.Sfix3 / via.sfix value-type fields
    private static readonly TypeDefinition? Sfix3Type = Tdb.FindType("via.Sfix3");
    private static readonly Field? Sfix3XField = Sfix3Type?.GetField("x");
    private static readonly Field? Sfix3YField = Sfix3Type?.GetField("y");
    private static readonly TypeDefinition? SfixType = Tdb.FindType("via.sfix");
    private static readonly Field? SfixVField = SfixType?.GetField("v");

    // ActionParam / AP_Collision
    private static readonly TypeDefinition? ActionParamType = Tdb.FindType("app.esports.ActionParam");
    private static readonly Field? ActionParamCollisionField = ActionParamType?.GetField("Collision");
    private static readonly TypeDefinition? ApCollisionType = Tdb.FindType("app.esports.AP_Collision");
    private static readonly Field? ApCollisionInfosField = ApCollisionType?.GetField("Infos");

    // Rect subtype TypeDefinitions (compared by native pointer/index for O(1) classification)
    private static readonly TypeDefinition? AtckInfoType = Tdb.FindType("nAction.AtckInfo");
    private static readonly TypeDefinition? DamgInfoType = Tdb.FindType("nAction.DamgInfo");
    private static readonly TypeDefinition? PushInfoType = Tdb.FindType("nAction.PushInfo");
    private static readonly TypeDefinition? UniqueInfoType = Tdb.FindType("nAction.UniqueInfo");

    private static readonly uint AtckInfoFqn = AtckInfoType?.FQN ?? 0;
    private static readonly uint DamgInfoFqn = DamgInfoType?.FQN ?? 0;
    private static readonly uint PushInfoFqn = PushInfoType?.FQN ?? 0;
    private static readonly uint UniqueInfoFqn = UniqueInfoType?.FQN ?? 0;

    // nAction.RectFix fields
    private static readonly TypeDefinition? RectFixType = Tdb.FindType("nAction.RectFix");
    private static readonly Field? RectOffsetXField = RectFixType?.GetField("OffsetX");
    private static readonly Field? RectOffsetYField = RectFixType?.GetField("OffsetY");
    private static readonly Field? RectSizeXField = RectFixType?.GetField("SizeX");
    private static readonly Field? RectSizeYField = RectFixType?.GetField("SizeY");

    // nAction.AtckInfo fields
    private static readonly Field? AtckCondFlagField = AtckInfoType?.GetField("CondFlag");
    private static readonly Field? AtckTypeFlagField = AtckInfoType?.GetField("TypeFlag");
    private static readonly Field? AtckPoseBitField = AtckInfoType?.GetField("PoseBit");
    private static readonly Field? AtckGuardBitField = AtckInfoType?.GetField("GuardBit");

    // nAction.DamgInfo fields
    private static readonly Field? DamgTypeFlagField = DamgInfoType?.GetField("TypeFlag");
    private static readonly Field? DamgTypeField = DamgInfoType?.GetField("Type");
    private static readonly Field? DamgImmuneField = DamgInfoType?.GetField("Immune");

    // Cached List<RectFix> fields (resolved lazily from the live List instance)
    private static Field? s_listItemsField;
    private static Field? s_listSizeField;

    public static bool TryGetPauseState(
        out ulong pauseTypeBit,
        out bool isHomePausing,
        out bool isSystemUiOverlaid)
    {
        pauseTypeBit = 0;
        isHomePausing = false;
        isSystemUiOverlaid = false;

        var pmMo = API.GetManagedSingleton("app.PauseManager");
        if (pmMo == null) return false;

        var pmAddr = pmMo.GetAddress();
        if (pmAddr == 0 || !ManagedObject.IsManagedObject(pmAddr)) return false;

        if (CurrentPauseTypeBitField != null)
            pauseTypeBit = (ulong)Marshal.ReadInt64((nint)CurrentPauseTypeBitField.GetDataRaw(pmAddr, false));

        if (IsHomePausingField != null)
            isHomePausing = Marshal.ReadByte((nint)IsHomePausingField.GetDataRaw(pmAddr, false)) != 0;

        if (IsSystemUiOverlaidField != null)
            isSystemUiOverlaid = Marshal.ReadByte((nint)IsSystemUiOverlaidField.GetDataRaw(pmAddr, false)) != 0;

        return true;
    }

    public static bool TryGetChronos(out int worldElapsed, out int worldNotch)
    {
        worldElapsed = 0;
        worldNotch = 0;

        if (ChronosWorldElapsedField == null || ChronosWorldNotchField == null) return false;

        var chronosMo = GetBattleStaticManagedObject(BattleChronosField, "Chronos");
        if (chronosMo == null) return false;

        var addr = chronosMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return false;

        worldElapsed = Marshal.ReadInt32((nint)ChronosWorldElapsedField.GetDataRaw(addr, false));
        worldNotch = Marshal.ReadInt32((nint)ChronosWorldNotchField.GetDataRaw(addr, false));
        return true;
    }

    public static bool TryGetStageTimer(out int stageTimer)
    {
        stageTimer = 0;
        if (SGameStageTimerField == null) return false;

        var gameMo = GetBattleStaticManagedObject(BattleGameField, "Game");
        if (gameMo == null) return false;

        var addr = gameMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return false;

        stageTimer = Marshal.ReadInt32((nint)SGameStageTimerField.GetDataRaw(addr, false));
        return true;
    }

    public static _System.Array? TryGetPlayersArray()
    {
        if (SPlayerMcPlayerField == null) return null;

        var playerMo = GetBattleStaticManagedObject(BattlePlayerField, "Player");
        if (playerMo == null) return null;

        var addr = playerMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return null;

        var arrMo = SPlayerMcPlayerField.GetDataBoxed(addr, false) as ManagedObject;
        if (arrMo == null || !ManagedObject.IsManagedObject(arrMo.GetAddress())) return null;

        return arrMo.As<_System.Array>();
    }

    public static _System.Array? TryGetGlobalWorkArray()
    {
        if (SWorkGlobalWorkField == null) return null;

        var workMo = GetBattleStaticManagedObject(BattleWorkField, "Work");
        if (workMo == null) return null;

        var addr = workMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return null;

        var arrMo = SWorkGlobalWorkField.GetDataBoxed(addr, false) as ManagedObject;
        if (arrMo == null || !ManagedObject.IsManagedObject(arrMo.GetAddress())) return null;

        return arrMo.As<_System.Array>();
    }

    public static bool TryGetEntityActParam(
        ManagedObject entityMo,
        out ManagedObject? actParamMo,
        out bool isDead,
        out int playerIndex)
    {
        actParamMo = null;
        isDead = false;
        playerIndex = -1;

        if (WorkActParamField == null) return false;

        var addr = entityMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return false;

        actParamMo = WorkActParamField.GetDataBoxed(addr, false) as ManagedObject;
        if (actParamMo == null || !ManagedObject.IsManagedObject(actParamMo.GetAddress()))
            return false;

        if (WorkIsR0DieMethod != null)
            isDead = WorkIsR0DieMethod.Invoke(entityMo, []).Byte != 0;

        if (isDead) return true;

        if (WorkIsTeam1PMethod != null && WorkIsTeam1PMethod.Invoke(entityMo, []).Byte != 0)
            playerIndex = 0;
        else if (WorkIsTeam2PMethod != null && WorkIsTeam2PMethod.Invoke(entityMo, []).Byte != 0)
            playerIndex = 1;

        return true;
    }

    public static bool TryReadEntityPosition(ManagedObject entityMo, out int posXV, out int posYV, out bool facingRight)
    {
        posXV = 0;
        posYV = 0;
        facingRight = true;

        if (WorkPosField == null || Sfix3XField == null || Sfix3YField == null) return false;

        var addr = entityMo.GetAddress();
        if (addr == 0 || !ManagedObject.IsManagedObject(addr)) return false;

        var posAddr = WorkPosField.GetDataRaw(addr, false);
        if (posAddr == 0) return false;

        var xAddr = Sfix3XField.GetDataRaw(posAddr, true);
        var yAddr = Sfix3YField.GetDataRaw(posAddr, true);
        posXV = ReadSfixRaw(xAddr);
        posYV = ReadSfixRaw(yAddr);

        if (WorkBitValueField != null)
        {
            var bitValue = (ushort)Marshal.ReadInt16((nint)WorkBitValueField.GetDataRaw(addr, false));
            facingRight = (bitValue & 0x80) != 0;
        }

        return true;
    }

    public static bool TryGetCollisionInfos(
        ManagedObject actParamMo,
        out _System.Array? itemsArray,
        out int count)
    {
        itemsArray = null;
        count = 0;

        if (ActionParamCollisionField == null || ApCollisionInfosField == null) return false;

        var actAddr = actParamMo.GetAddress();
        if (actAddr == 0 || !ManagedObject.IsManagedObject(actAddr)) return false;

        var colMo = ActionParamCollisionField.GetDataBoxed(actAddr, false) as ManagedObject;
        if (colMo == null) return false;

        var colAddr = colMo.GetAddress();
        if (colAddr == 0 || !ManagedObject.IsManagedObject(colAddr)) return false;

        var listMo = ApCollisionInfosField.GetDataBoxed(colAddr, false) as ManagedObject;
        if (listMo == null) return false;

        var listAddr = listMo.GetAddress();
        if (listAddr == 0 || !ManagedObject.IsManagedObject(listAddr)) return false;

        if (s_listItemsField == null || s_listSizeField == null)
        {
            var listTd = listMo.GetTypeDefinition();
            s_listItemsField = listTd?.GetField("_items");
            s_listSizeField = listTd?.GetField("_size");
            if (s_listItemsField == null || s_listSizeField == null) return false;
        }

        count = Marshal.ReadInt32((nint)s_listSizeField.GetDataRaw(listAddr, false));
        if (count <= 0) return true;

        var itemsMo = s_listItemsField.GetDataBoxed(listAddr, false) as ManagedObject;
        if (itemsMo == null || !ManagedObject.IsManagedObject(itemsMo.GetAddress())) return false;

        itemsArray = itemsMo.As<_System.Array>();
        if (itemsArray == null) return false;

        if (count > itemsArray.Length) count = itemsArray.Length;
        return true;
    }

    public static BattleRectKind ClassifyRect(ManagedObject rectMo)
    {
        var td = rectMo.GetTypeDefinition();
        if (td == null) return BattleRectKind.Unknown;

        var fqn = td.FQN;
        if (fqn == AtckInfoFqn) return BattleRectKind.Atck;
        if (fqn == DamgInfoFqn) return BattleRectKind.Damg;
        if (fqn == PushInfoFqn) return BattleRectKind.Push;
        if (fqn == UniqueInfoFqn) return BattleRectKind.Unique;

        // Fallback if inheritance or variant type appears
        if (AtckCondFlagField != null && td.IsDerivedFrom("nAction.AtckInfo")) return BattleRectKind.Atck;
        if (DamgTypeFlagField != null && td.IsDerivedFrom("nAction.DamgInfo")) return BattleRectKind.Damg;
        if (PushInfoType != null && td.IsDerivedFrom("nAction.PushInfo")) return BattleRectKind.Push;
        if (UniqueInfoType != null && td.IsDerivedFrom("nAction.UniqueInfo")) return BattleRectKind.Unique;

        return BattleRectKind.Unknown;
    }

    public static bool TryReadRectGeometry(
        ulong rectAddr,
        out int offsetX,
        out int offsetY,
        out int sizeX,
        out int sizeY)
    {
        offsetX = 0;
        offsetY = 0;
        sizeX = 0;
        sizeY = 0;

        if (RectOffsetXField == null || RectOffsetYField == null || RectSizeXField == null || RectSizeYField == null)
            return false;

        offsetX = ReadSfixRaw(RectOffsetXField.GetDataRaw(rectAddr, false));
        offsetY = ReadSfixRaw(RectOffsetYField.GetDataRaw(rectAddr, false));
        sizeX = ReadSfixRaw(RectSizeXField.GetDataRaw(rectAddr, false));
        sizeY = ReadSfixRaw(RectSizeYField.GetDataRaw(rectAddr, false));
        return true;
    }

    public static void ReadAtckInfo(
        ulong rectAddr,
        out uint condFlag,
        out ushort typeFlag,
        out byte poseBit,
        out byte guardBit)
    {
        condFlag = AtckCondFlagField != null
            ? (uint)Marshal.ReadInt32((nint)AtckCondFlagField.GetDataRaw(rectAddr, false))
            : 0u;
        typeFlag = AtckTypeFlagField != null
            ? (ushort)Marshal.ReadInt16((nint)AtckTypeFlagField.GetDataRaw(rectAddr, false))
            : (ushort)0;
        poseBit = AtckPoseBitField != null
            ? Marshal.ReadByte((nint)AtckPoseBitField.GetDataRaw(rectAddr, false))
            : (byte)0;
        guardBit = AtckGuardBitField != null
            ? Marshal.ReadByte((nint)AtckGuardBitField.GetDataRaw(rectAddr, false))
            : (byte)0;
    }

    public static void ReadDamgInfo(
        ulong rectAddr,
        out ushort typeFlag,
        out short damgType,
        out byte immune)
    {
        typeFlag = DamgTypeFlagField != null
            ? (ushort)Marshal.ReadInt16((nint)DamgTypeFlagField.GetDataRaw(rectAddr, false))
            : (ushort)0;
        damgType = DamgTypeField != null
            ? Marshal.ReadInt16((nint)DamgTypeField.GetDataRaw(rectAddr, false))
            : (short)0;
        immune = DamgImmuneField != null
            ? Marshal.ReadByte((nint)DamgImmuneField.GetDataRaw(rectAddr, false))
            : (byte)0;
    }

    private static ManagedObject? GetBattleStaticManagedObject(Field? staticField, string fieldName)
    {
        if (staticField != null)
            return staticField.GetDataBoxed(0, false) as ManagedObject;

        return BattleType?.Statics?.GetField(fieldName) as ManagedObject;
    }

    private static int ReadSfixRaw(ulong sfixAddr)
    {
        if (sfixAddr == 0) return 0;
        var vAddr = SfixVField != null ? SfixVField.GetDataRaw(sfixAddr, true) : sfixAddr;
        return Marshal.ReadInt32((nint)vAddr);
    }
}