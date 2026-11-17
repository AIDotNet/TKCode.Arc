namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="MemoryClassifier"/>, the built-in guess behind the add field: people first (known names, or two
/// capitalised words after the first word), then preference words, then project words, else the first group; guesses the
/// card does not have fall back to its first group.
/// </summary>
public class MemoryClassifierTests
{
    private static readonly string[] s_defaults = ["about", "preferences", "projects", "people"];

    [Theory]
    [InlineData("Leads the data platform team at a regional freight company", "about")]
    [InlineData("Is training for a spring half marathon", "about")]
    [InlineData("Writes most services in C# and some tooling in Go", "about")]
    [InlineData("Wants a code sample before the explanation", "preferences")]
    [InlineData("Prefers tables to long bullet lists", "preferences")]
    [InlineData("Uses British spelling in written documents", "preferences")]
    [InlineData("Is moving the depot planner to Avalonia this quarter", "projects")]
    [InlineData("Is drafting a proposal on event sourcing for the billing team", "projects")]
    [InlineData("Mina Okafor reviews every database migration", "people")]
    [InlineData("Pairs with Tomás Ferreira on the routing service", "people")]
    [InlineData("Meets Ingrid Holm for a one-to-one every other Tuesday", "people")]
    [InlineData("Runs builds with the dotnet CLI rather than the IDE", "preferences")]
    public void The_Sample_Memories_Land_In_Their_Groups(string text, string expected)
    {
        Assert.Equal(expected, MemoryClassifier.Classify(text, s_defaults));
    }

    [Fact]
    public void People_Win_Over_Preference_And_Project_Words()
    {
        Assert.Equal("people", MemoryClassifier.Classify("Prefers to plan the project with Ana Lima", s_defaults));
        Assert.Equal("preferences", MemoryClassifier.Classify("Prefers to plan the project alone", s_defaults));
    }

    [Fact]
    public void The_Sentence_Start_Is_Not_Half_A_Name()
    {
        // "Uses British": the first word is capitalised only because it starts the sentence.
        Assert.False(MemoryClassifier.MentionsPerson("Uses British spelling"));
        Assert.True(MemoryClassifier.MentionsPerson("Works with Ana Lima"));
        // Acronyms are not names.
        Assert.False(MemoryClassifier.MentionsPerson("Runs the CLI IDE combo"));
        // A known first name counts anywhere, even first.
        Assert.True(MemoryClassifier.MentionsPerson("Ingrid prefers mornings"));
    }

    [Fact]
    public void Words_Match_Whole_And_Without_Case()
    {
        Assert.Equal("preferences", MemoryClassifier.Classify("ALWAYS answers in short paragraphs", s_defaults));
        // "projector" is not "project"; "users" is not "uses".
        Assert.Equal("about", MemoryClassifier.Classify("Owns a projector for the users group", s_defaults));
        Assert.Equal("about", MemoryClassifier.Classify("", s_defaults));
        Assert.Equal("about", MemoryClassifier.Classify(null, s_defaults));
    }

    [Fact]
    public void A_Guess_The_Card_Does_Not_Have_Falls_Back_To_The_First_Group()
    {
        string[] custom = ["work", "home"];
        Assert.Equal("work", MemoryClassifier.Classify("Prefers tea", custom));
        Assert.Equal("work", MemoryClassifier.Classify("Lives near the sea", custom));
        string[] some = ["home", "people"];
        Assert.Equal("people", MemoryClassifier.Classify("Calls Mina every Friday", some));
        // Without groups to choose from, the guess itself (about when nothing matches).
        Assert.Equal("preferences", MemoryClassifier.Classify("Prefers tea", []));
        Assert.Equal("about", MemoryClassifier.Classify("Lives near the sea", []));
        Assert.Equal("preferences", MemoryClassifier.Guess("Prefers tea"));
    }

    [Fact]
    public void Words_Keep_Inner_Apostrophes()
    {
        var text = "Doesn't like Mondays";
        var words = MemoryClassifier.Words(text).Select(r => text.Substring(r.Start, r.Length)).ToArray();
        Assert.Equal(["Doesn't", "like", "Mondays"], words);
    }
}
