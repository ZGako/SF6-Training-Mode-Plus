using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public class HealthBar : IRandomizableFeature
{
    // TODO: make this take a player "index" parameter, save it as an instance parameter.
    // Change stuff accordingly (to not have to copy paste code or whatever)
    private HealthBar()
    {
    }

    public static HealthBar Instance { get; private set; } = new();

    public void Init()
    {
        throw new NotImplementedException();
    }

    public void Unload()
    {
        throw new NotImplementedException();
    }

    public bool IsRandomizedEnabled { get; private set; }

    public void Randomize()
    {
        throw new NotImplementedException();
    }
}