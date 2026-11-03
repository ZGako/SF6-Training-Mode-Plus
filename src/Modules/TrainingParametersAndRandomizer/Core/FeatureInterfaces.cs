namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

public interface ITrainingFeature
{
    public void Init();

    public void Unload();
}

public interface IRandomizableFeature : ITrainingFeature
{
    public bool IsRandomizedEnabled { get; }
    public void Randomize();
}