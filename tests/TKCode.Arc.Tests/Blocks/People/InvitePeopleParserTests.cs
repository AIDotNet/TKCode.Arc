using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// The pure parts of <see cref="InvitePeople"/> on the invented Lumen Labs data: splitting and recognizing pasted lists,
/// the seven error kinds, duplicates, the suggestion filter (substring of name or email, available first, cut to five,
/// four for an empty field, the "invite by email" row), seat fractions and the shake curve.
/// </summary>
public class InvitePeopleParserTests
{
    private static (InvitePeopleRoster Roster, InvitePeopleDemoData.People People) Lumen()
    {
        var people = new InvitePeopleDemoData.People();
        var theo = new InvitePeoplePendingInvite(people.Theo, null, "3 days ago");
        return (new InvitePeopleRoster(people.Directory, people.Team, [theo.Person]), people);
    }

    [Fact]
    public void Lists_Split_At_Commas_Semicolons_And_Line_Breaks_Only()
    {
        InvitePeopleParser.Split("a, b;c\nd\r\ne,, ;").ShouldBe(["a", "b", "c", "d", "e"]);
        InvitePeopleParser.Split("a@x.io b@x.io\tc").ShouldBe(["a@x.io b@x.io\tc"]);
        InvitePeopleParser.HasSeparator("one two").ShouldBeFalse();
        InvitePeopleParser.HasSeparator("one\ntwo").ShouldBeTrue();
    }

    [Theory]
    [InlineData("nobody", InvitePeopleEntryError.NotEmail)]
    [InlineData("@lumen.example", InvitePeopleEntryError.MissingLocal)]
    [InlineData("hugo.lambert@", InvitePeopleEntryError.MissingDomain)]
    [InlineData("nadia@lumenlabs", InvitePeopleEntryError.IncompleteDomain)]
    [InlineData("a@b.c", InvitePeopleEntryError.Invalid)]
    [InlineData("a b@lumen.example", InvitePeopleEntryError.Invalid)]
    [InlineData("a@@lumen.example", InvitePeopleEntryError.Invalid)]
    [InlineData("mailto:a@lumen.example", InvitePeopleEntryError.Invalid)]
    [InlineData("a@lumen..example", InvitePeopleEntryError.Invalid)]
    [InlineData("sam@rivers.co", InvitePeopleEntryError.None)]
    [InlineData("x@site.museum", InvitePeopleEntryError.None)]
    public void Addresses_Are_Checked_For_Their_Shape(string text, InvitePeopleEntryError expected)
    {
        InvitePeopleParser.ValidateEmail(text).ShouldBe(expected);
    }

    [Fact]
    public void Names_Addresses_And_Angle_Brackets_Are_Recognized()
    {
        var (roster, people) = Lumen();
        InvitePeopleParser.Recognize("  clara   WEISS ", roster).Person.ShouldBe(people.Clara);
        InvitePeopleParser.Recognize("Clara", roster).Error.ShouldBe(InvitePeopleEntryError.NotEmail);
        InvitePeopleParser.Recognize("MATEUS.ROCHA@lumen.example", roster).Person.ShouldBe(people.Mateus);
        var bracket = InvitePeopleParser.Recognize("Ravi M <ravi.menon@lumen.example>", roster);
        bracket.Person.ShouldBe(people.Ravi);
        bracket.IsValid.ShouldBeTrue();
        var fresh = InvitePeopleParser.Recognize("Sam <sam@rivers.co>", roster);
        fresh.IsValid.ShouldBeTrue();
        fresh.Person!.Name.ShouldBeNull();
        fresh.Text.ShouldBe("sam@rivers.co");
        InvitePeopleParser.Recognize("Sam <sam@rivers>", roster).Error.ShouldBe(InvitePeopleEntryError.IncompleteDomain);
        var member = InvitePeopleParser.Recognize("kenji.ito@lumen.example", roster);
        member.Error.ShouldBe(InvitePeopleEntryError.AlreadyMember);
        member.Text.ShouldBe("Kenji Ito");
        InvitePeopleParser.Recognize("Theo Grant", roster).Error.ShouldBe(InvitePeopleEntryError.AlreadyInvited);
    }

    [Fact]
    public void The_Sample_List_Adds_Five_Flags_Four_And_Skips_Two()
    {
        var (roster, people) = Lumen();
        var result = InvitePeopleParser.Parse(InvitePeopleDemoData.SampleList, roster);
        result.ValidCount.ShouldBe(5);
        result.InvalidCount.ShouldBe(4);
        result.Duplicates.ShouldBe(2);
        result.Entries.Where(e => e.IsValid).Select(e => e.Person).ShouldBe([people.Clara, people.Mateus, people.Ravi, people.Mina, people.Elif]);
        result.Entries.Where(e => !e.IsValid).Select(e => e.Error).ShouldBe([
            InvitePeopleEntryError.AlreadyMember, InvitePeopleEntryError.IncompleteDomain,
            InvitePeopleEntryError.MissingDomain, InvitePeopleEntryError.NotEmail
        ]);
    }

    [Fact]
    public void Entries_Already_In_The_Field_Count_As_Duplicates()
    {
        var (roster, _) = Lumen();
        var first = InvitePeopleParser.Parse("Clara Weiss, nobody", roster);
        var again = InvitePeopleParser.Parse("clara.weiss@lumen.example; NOBODY; Jonas Berg", roster, first.Entries.Select(e => e.Key));
        again.Duplicates.ShouldBe(2);
        again.Entries.Single().Text.ShouldBe("Jonas Berg");
    }

    [Fact]
    public void An_Empty_Query_Suggests_The_First_Four_People_Who_Can_Be_Invited()
    {
        var (roster, people) = Lumen();
        var list = InvitePeopleSuggestions.Build("", roster, new HashSet<string>(), 5, 4);
        list.IsSuggested.ShouldBeTrue();
        list.Rows.Select(r => r.Person).ShouldBe([people.Clara, people.Mateus, people.Nadia, people.Hugo]);
        var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { people.Clara.Email! };
        InvitePeopleSuggestions.Build("", roster, picked, 5, 4).Rows[0].Person.ShouldBe(people.Mateus);
    }

    [Fact]
    public void Queries_Match_Names_Or_Emails_With_Available_People_First()
    {
        var (roster, people) = Lumen();
        var list = InvitePeopleSuggestions.Build("is", roster, new HashSet<string>(), 5, 4);
        list.Rows.Select(r => r.Person).ShouldBe([people.Clara, people.Sara, people.Iris, people.Lena]);
        list.Rows.Select(r => r.Block).ShouldBe([
            InvitePeopleEntryError.None, InvitePeopleEntryError.None, InvitePeopleEntryError.AlreadyMember,
            InvitePeopleEntryError.AlreadyMember
        ]);
        // Every address contains "lumen": five rows at most, all of them people who can be invited.
        var many = InvitePeopleSuggestions.Build("LUMEN", roster, new HashSet<string>(), 5, 4);
        many.Rows.Count.ShouldBe(5);
        many.Rows.ShouldAllBe(r => !r.IsBlocked);
        // The job title is not searched; the query is not trimmed.
        InvitePeopleSuggestions.Build("designer", roster, new HashSet<string>(), 5, 4).Rows.ShouldBeEmpty();
        InvitePeopleSuggestions.Build("clara  weiss", roster, new HashSet<string>(), 5, 4).Rows.ShouldBeEmpty();
        InvitePeopleSuggestions.Build("theo", roster, new HashSet<string>(), 5, 4).Rows.Single().Block
            .ShouldBe(InvitePeopleEntryError.AlreadyInvited);
    }

    [Fact]
    public void A_New_Address_Offers_One_Invite_By_Email_Row()
    {
        var (roster, people) = Lumen();
        var list = InvitePeopleSuggestions.Build("sam@rivers.co", roster, new HashSet<string>(), 5, 4);
        list.Rows.Single().Kind.ShouldBe(InvitePeopleSuggestionKind.Email);
        list.Rows[0].Email.ShouldBe("sam@rivers.co");
        // A directory address finds the person instead.
        InvitePeopleSuggestions.Build(people.Clara.Email, roster, new HashSet<string>(), 5, 4).Rows.Single().Person.ShouldBe(people.Clara);
    }

    [Fact]
    public void Seats_Stack_Members_Pending_And_New()
    {
        var seats = new InvitePeopleSeats(5, 1, 5, 10);
        seats.Total.ShouldBe(11);
        seats.IsOver.ShouldBeTrue();
        seats.Over.ShouldBe(1);
        seats.MembersFraction.ShouldBe(0.5);
        seats.PendingFraction.ShouldBe(0.6);
        seats.NewFraction.ShouldBe(1);
        new InvitePeopleSeats(5, 1, 4, 10).IsOver.ShouldBeFalse();
    }

    [Fact]
    public void The_Shake_Passes_Its_Keys_In_Equal_Steps()
    {
        InvitePeopleShake.Sample(0).ShouldBe(0);
        InvitePeopleShake.Sample(1 / 6.0).ShouldBe(6, 1e-9);
        InvitePeopleShake.Sample(2 / 6.0).ShouldBe(-6, 1e-9);
        InvitePeopleShake.Sample(3 / 6.0).ShouldBe(4, 1e-9);
        InvitePeopleShake.Sample(5 / 6.0).ShouldBe(2, 1e-9);
        InvitePeopleShake.Sample(1).ShouldBe(0);
        InvitePeopleShake.Duration.ShouldBe(TimeSpan.FromMilliseconds(420));
    }
}
