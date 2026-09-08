namespace SF6_Training_Mode_Plus;

using REFrameworkNET;
using REFrameworkNET.Attributes;


public class TrainingModePlus
{
    [PluginEntryPoint]
    public static void Main()
    {
        API.LogInfo("Hello from C#!");
    }

    [PluginExitPoint]
    public static void OnUnload()
    {
        API.LogInfo("Bye from C#!");
    }

}
