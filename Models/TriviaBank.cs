namespace ChastiMaid.Models;

public record TriviaQuestion(string Question, List<string> Answers, int CorrectIndex);

public static class TriviaBank
{
    private static readonly Random Rng = new();

    private static readonly List<TriviaQuestion> Questions =
    [
        new("What is the capital of France?", ["Berlin", "Madrid", "Paris", "Rome"], 2),
        new("How many minutes are in one hour?", ["30", "60", "90", "120"], 1),
        new("What color do you get mixing red and white?", ["Orange", "Pink", "Purple", "Yellow"], 1),
        new("Which planet is closest to the Sun?", ["Venus", "Earth", "Mercury", "Mars"], 2),
        new("How many sides does a triangle have?", ["2", "3", "4", "5"], 1),
        new("What is 7 × 8?", ["54", "56", "58", "64"], 1),
        new("Which ocean is the largest?", ["Atlantic", "Indian", "Arctic", "Pacific"], 3),
        new("What gas do plants absorb?", ["Oxygen", "Nitrogen", "Carbon dioxide", "Helium"], 2),
        new("How many days are in a leap year?", ["364", "365", "366", "367"], 2),
        new("What is the boiling point of water in °C?", ["90", "95", "100", "110"], 2)
    ];

    public static TriviaQuestion GetRandom()
    {
        return Questions[Rng.Next(Questions.Count)];
    }
}
