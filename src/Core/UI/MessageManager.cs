using System;
using System.Runtime.InteropServices;
using REFrameworkNET;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET.Collections;
using System.Collections.Generic;

namespace SF6_Training_Mode_Plus.Core.UI;

public class CustomMessage
{
    public Guid Id { get; }

    // raw C# strings for each language
    private readonly string[] _translations = new string[sizeof(via.Language)];

    // Cache the Engine-allocated ManagedObject strings
    private readonly ManagedObject[] _cachedEngineStrings = new ManagedObject[sizeof(via.Language)];

    public CustomMessage(string englishFallback)
    {
        Id = Guid.NewGuid();
        // Fill the array with the fallback text so missing translations don't show up blank
        Array.Fill(_translations, englishFallback);

        MessageManager.RegisterCustomMessage(this);
    }

    // Builder pattern method to easily chain translations in your init phase
    public CustomMessage AddTranslation(via.Language language, string text)
    {
        _translations[(int)language] = text;
        return this;
    }

    // Retrieves the raw memory address of the engine string to replace 'retval'
    public ulong GetEngineStringAddress()
    {
        int langIndex = (int)app.helper.hGUI.GetSystemLanguage();

        // Lazy-load: Only allocate in the engine if it's actually requested
        if (_cachedEngineStrings[langIndex] == null)
        {
            // Allocate the string on the RE Engine's managed GC heap
            var engineString = VM.CreateString(_translations[langIndex]);
            engineString.Globalize();

            _cachedEngineStrings[langIndex] = engineString;
        }

        return _cachedEngineStrings[langIndex].GetAddress();
    }
}

public static class MessageManager
{

    private static readonly Dictionary<Guid, CustomMessage> CustomMessages = [];

    [ThreadStatic]
    private static CustomMessage? s_pendingMessageOverride;

    public static void SetGuid(ManagedObject obj, string fieldName, Guid guid)
    {
        // Get the field definition to find its memory offset
        var typeDef = obj.GetTypeDefinition();
        var field = typeDef.GetField(fieldName);

        if (field == null)
        {
            API.LogWarning($"Field {fieldName} not found on {typeDef.FullName}");
            return;
        }

        // Get the byte offset from the object's base address
        uint offset = field.OffsetFromBase;

        // Calculate the exact memory destination as an IntPtr
        IntPtr destAddress = (IntPtr)(obj.GetAddress() + offset);

        // Convert the standard C# Guid into a 16-byte array
        byte[] guidBytes = guid.ToByteArray();

        // Copy the bytes directly into the engine's memory
        Marshal.Copy(guidBytes, 0, destAddress, 16);
    }

    public static void RegisterCustomMessage(CustomMessage message)
    {
        if (!CustomMessages.ContainsKey(message.Id))
        {
            CustomMessages[message.Id] = message;
        }
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Pre)]
    private static PreHookResult ReplaceMessageQuery(Span<ulong> args)
    {
        // read argument
        IntPtr guidAddress = (IntPtr)args[1];
        Guid requestedGuid = Marshal.PtrToStructure<Guid>(guidAddress);

        // API.LogInfo($"getMessage called with GUID: {requestedGuid}");

        // Check if we have a custom message for this GUID
        if (CustomMessages.TryGetValue(requestedGuid, out var customMsg))
        {
            s_pendingMessageOverride = customMsg;
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Post)]
    private static void ReplaceMessageResponse(ref ulong retval)
    {
        if (s_pendingMessageOverride != null)
        {
            // Replace the return value with the memory address of our cached Engine String
            retval = s_pendingMessageOverride.GetEngineStringAddress();

            // Reset the override for the next call to prevent race conditions
            s_pendingMessageOverride = null;
        }
    }

    public static void Clear()
    {
        CustomMessages.Clear();
    }
}