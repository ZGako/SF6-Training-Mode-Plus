using System;

using REFrameworkNET;

namespace SF6_Training_Mode_Plus.Core.UI;

public static class UIHelpers
{

    public static T GetArgAs<T>(ulong address) where T : class
    {
        var managedObj = ManagedObject.ToManagedObject(address)
            ?? throw new ArgumentException($"Failed to convert pointer address {address} to a ManagedObject.");

        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
        {
            if (!actualType.IsDerivedFrom(requestedType))
            {
                throw new InvalidCastException(
                    $"Strict Cast Failed: Object at address {address} is of type '{actualType.FullName}', " +
                    $"which does not match or derive from the requested type '{requestedType.FullName}'.");
            }
        }
        else
        {
            API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        }

        return managedObj.As<T>() ?? throw new InvalidCastException($"Proxy creation failed for object at address {address}.");
    }

    private static class TypeDefCache<T> where T : class
    {
        public static readonly TypeDefinition? RequestedType;

        static TypeDefCache()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

            // Your exact reflection logic, but it only ever runs once!
            RequestedType = (typeof(T).GetField("REFType", flags)?.GetValue(null)
                          ?? typeof(T).GetProperty("REFType", flags)?.GetValue(null)) as TypeDefinition;
        }
    }
}