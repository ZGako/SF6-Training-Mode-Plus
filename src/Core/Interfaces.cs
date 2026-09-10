

namespace SF6_Training_Mode_Plus.Core;

public interface ITrainingModePlusModule
{
    void Init();

    void Unload();
}


public interface IDynamicUIModifier
{
    void Restore();
}