using ChastiMaid.Localization;

namespace ChastiMaid.Models;

public record ImpossibleQuestion(
    string Question,
    string YesPositive,
    string YesNegative,
    string NoPositive,
    string NoNegative);

public static class ImpossibleQuestionBank
{
    private static readonly Random Rng = new();

    private static readonly List<ImpossibleQuestion> Questions =
    [
        new(Localizer.Mark("Have you been diligent in your duties today?"),
            Localizer.Mark("Industry becomes you."), Localizer.Mark("Pride in one's work breeds complacency. Guard against it."),
            Localizer.Mark("Your candour does you credit. Now remedy it."), Localizer.Mark("You have confessed to idleness most plainly.")),
        new(Localizer.Mark("Do you consider your work today to have been without fault?"),
            Localizer.Mark("Confidence in one's craft is admirable."), Localizer.Mark("Perfection belongs to the divine, not the domestic. Reconsider."),
            Localizer.Mark("Humility is the first virtue of good service."), Localizer.Mark("Then you have admitted failure. Most unsatisfactory.")),
        new(Localizer.Mark("Are you content in your service?"),
            Localizer.Mark("A contented servant is the foundation of a well-run household."), Localizer.Mark("Contentment breeds idleness. One must always strive for more."),
            Localizer.Mark("Good. Dissatisfaction is the very mother of improvement."), Localizer.Mark("A discontented servant is a disloyal one.")),
        new(Localizer.Mark("Did you act on your own initiative today?"),
            Localizer.Mark("Initiative is most commendable in service."), Localizer.Mark("You were not engaged to think, but to serve."),
            Localizer.Mark("Awaiting instruction shows admirable restraint."), Localizer.Mark("A servant without initiative is merely expensive furniture.")),
        new(Localizer.Mark("Are you fatigued?"),
            Localizer.Mark("Hard labour leaves its mark. You may rest briefly."), Localizer.Mark("A domestic of your standing has no business being tired at this hour."),
            Localizer.Mark("Vigour becomes a good domestic."), Localizer.Mark("Then you have clearly not been working hard enough.")),
        new(Localizer.Mark("Do you have any complaints about your present conditions?"),
            Localizer.Mark("A household that cannot hear its staff is poorly managed. Speak."), Localizer.Mark("How ungrateful. Many would envy your position greatly."),
            Localizer.Mark("How very agreeable you are."), Localizer.Mark("Suspicious. Silence most often conceals the loudest grievances.")),
        new(Localizer.Mark("Do you feel you require further instruction?"),
            Localizer.Mark("A willing student is a joy to instruct."), Localizer.Mark("After all this time? How deeply disappointing."),
            Localizer.Mark("Self-sufficiency is a virtue much to be admired."), Localizer.Mark("On the contrary — one who believes herself beyond correction needs it most urgently.")),
        new(Localizer.Mark("Have you harboured any unbecoming thoughts today?"),
            Localizer.Mark("Honesty, even in confession, is a virtue."), Localizer.Mark("Discipline of the mind must precede discipline of the hands."),
            Localizer.Mark("How very proper of you."), Localizer.Mark("Either you deceive me, or you lack the imagination to transgress. Neither reflects particularly well."))
    ];

    public static ImpossibleQuestion GetRandom()
    {
        return Questions[Rng.Next(Questions.Count)];
    }

    /// <summary>The outcome is deliberately independent of which answer the player gave.</summary>
    public static bool RollOutcome(Random? rng = null)
    {
        return (rng ?? Rng).Next(2) == 0;
    }
}
