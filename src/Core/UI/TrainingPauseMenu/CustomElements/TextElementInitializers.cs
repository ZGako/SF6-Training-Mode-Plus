namespace SF6_TMP.Core.UI.TrainingPauseMenu.CustomElements;

public class RowButtonInitializer(CustomMessage message, CustomMessage guideMessage) : CustomElementBase(new CustomElementInitializer
{
    Type = app.training.ItemType.TEXT_ONLY,
    MessageID = message,
    GuideMessage = guideMessage,
    IsEnabled = true,
});

public class SpinnerOptionTextInitializer(CustomMessage message) : CustomElementBase(new CustomElementInitializer
{
    Type = app.training.ItemType.TEXT_ONLY,
    MessageID = message,
    IsEnabled = true,
});