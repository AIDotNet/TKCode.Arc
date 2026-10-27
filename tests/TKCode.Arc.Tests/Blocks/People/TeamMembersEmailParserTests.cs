using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>The invite field's rules (<see cref="TeamMembersEmailParser"/>) without UI.</summary>
public class TeamMembersEmailParserTests
{
    private static readonly Dictionary<string, string> s_members = new(StringComparer.OrdinalIgnoreCase)
    {
        ["theo@northwind.example"] = "Theo Brandt",
    };

    private static readonly HashSet<string> s_invited = new(StringComparer.OrdinalIgnoreCase) { "owen.park@northwind.example" };

    private static string? MemberName(string email) => s_members.GetValueOrDefault(email);

    private static bool Invited(string email) => s_invited.Contains(email);

    [Fact]
    public void SplitsAtCommasSemicolonsSpacesAndLineBreaks()
    {
        TeamMembersEmailParser.Split("a1@x.io,b2@x.io;c3@x.io d4@x.io\ne5@x.io\r\n, ,")
            .ShouldBe(["a1@x.io", "b2@x.io", "c3@x.io", "d4@x.io", "e5@x.io"]);
        TeamMembersEmailParser.Split(null).ShouldBeEmpty();
        TeamMembersEmailParser.HasSeparator("a@b.co").ShouldBeFalse();
        TeamMembersEmailParser.HasSeparator("a@b.co;").ShouldBeTrue();
    }

    [Theory]
    [InlineData("a1@x.io", true)]
    [InlineData("zed@acme.io", true)]
    [InlineData("first.last@sub.domain.example", true)]
    [InlineData("notanemail", false)]
    [InlineData("bad", false)]
    [InlineData("me@host", false)]
    [InlineData("a@b.c", false)]
    [InlineData("bad@@x", false)]
    [InlineData("@x.io", false)]
    [InlineData("a@.io", false)]
    [InlineData("a@x..io", false)]
    [InlineData("a b@x.io", false)]
    public void ChecksAddresses(string email, bool valid) => TeamMembersEmailParser.IsValid(email).ShouldBe(valid);

    [Theory]
    [InlineData("owen.park@northwind.example", "OP")]
    [InlineData("lena@corp.io", "L")]
    [InlineData("sara_lind-x@coastal.studio", "SL")]
    [InlineData("", "?")]
    public void DerivesInitials(string email, string initials) => TeamMembersEmailParser.InitialsOf(email).ShouldBe(initials);

    [Fact]
    public void ValidNewPiecesBecomeChipsAndDuplicatesDropSilently()
    {
        var result = TeamMembersEmailParser.Commit(["a1@x.io", "A1@X.io", "b2@x.io", "zed@acme.io"], ["zed@acme.io"], MemberName, Invited);
        result.Added.ShouldBe(["a1@x.io", "b2@x.io"]);
        result.Draft.ShouldBeEmpty();
        result.Error.ShouldBe(TeamMembersEmailError.None);
    }

    [Fact]
    public void InvalidPiecesStayJoinedAndWinOverMemberErrors()
    {
        var result = TeamMembersEmailParser.Commit(["bad", "theo@northwind.example", "ok@x.io", "me@host"], [], MemberName, Invited);
        result.Added.ShouldBe(["ok@x.io"]);
        result.Draft.ShouldBe("bad, me@host");
        result.Error.ShouldBe(TeamMembersEmailError.Invalid);
        result.ErrorArgument.ShouldBe("bad");
    }

    [Fact]
    public void MembersAndPendingInvitesDropWithTheirOwnErrors()
    {
        var member = TeamMembersEmailParser.Commit(["THEO@northwind.example"], [], MemberName, Invited);
        member.Added.ShouldBeEmpty();
        member.Error.ShouldBe(TeamMembersEmailError.AlreadyMember);
        member.ErrorArgument.ShouldBe("Theo Brandt");

        var invited = TeamMembersEmailParser.Commit(["owen.park@northwind.example", "theo@northwind.example"], [], MemberName, Invited);
        invited.Error.ShouldBe(TeamMembersEmailError.AlreadyInvited);
        invited.ErrorArgument.ShouldBe("owen.park@northwind.example");
    }

    [Fact]
    public void ACustomValidatorReplacesTheBuiltInCheck()
    {
        var result = TeamMembersEmailParser.Commit(["me@host", "a@x.io"], [], MemberName, Invited, e => e.EndsWith("@host"));
        result.Added.ShouldBe(["me@host"]);
        result.Draft.ShouldBe("a@x.io");
    }

    [Fact]
    public void SendCountAddsAValidNewDraft()
    {
        TeamMembersEmailParser.SendCount(["a@x.io"], "b@x.io", MemberName, Invited).ShouldBe(2);
        TeamMembersEmailParser.SendCount(["a@x.io"], "bad", MemberName, Invited).ShouldBe(1);
        TeamMembersEmailParser.SendCount(["a@x.io"], "A@x.io", MemberName, Invited).ShouldBe(1);
        TeamMembersEmailParser.SendCount([], "theo@northwind.example", MemberName, Invited).ShouldBe(0);
        TeamMembersEmailParser.SendCount([], "  ", MemberName, Invited).ShouldBe(0);
    }

    [Fact]
    public void SeatNoticeLooksAtTheChipsOnly()
    {
        TeamMembersEmailParser.SeatNotice(3, 3).ShouldBe(TeamMembersSeatNotice.None);
        TeamMembersEmailParser.SeatNotice(3, 5).ShouldBe(TeamMembersSeatNotice.OverLimit);
        TeamMembersEmailParser.SeatNotice(0, 0).ShouldBe(TeamMembersSeatNotice.Full);
        TeamMembersEmailParser.SeatNotice(0, 1).ShouldBe(TeamMembersSeatNotice.FullWithChips);
    }

    [Fact]
    public void SendNeedsSomethingToSendAndRoomForTheChips()
    {
        TeamMembersEmailParser.CanSend(0, 3, 0).ShouldBeFalse();
        TeamMembersEmailParser.CanSend(1, 3, 0).ShouldBeTrue();
        TeamMembersEmailParser.CanSend(4, 3, 3).ShouldBeTrue();
        TeamMembersEmailParser.CanSend(5, 3, 5).ShouldBeFalse();
        TeamMembersEmailParser.CanSend(1, 0, 0).ShouldBeFalse();
    }

    [Fact]
    public void SearchMatchesSubstringsIgnoringCase()
    {
        TeamMembersEmailParser.Matches("RA", "Ravi Malhotra", "ravi@x.io").ShouldBeTrue();
        TeamMembersEmailParser.Matches("coastal", null, "sara.lind@coastal.studio").ShouldBeTrue();
        TeamMembersEmailParser.Matches("zzq", "Lena Hart", "lena@x.io").ShouldBeFalse();
        TeamMembersEmailParser.Matches(" ", "anything").ShouldBeTrue();
    }
}
