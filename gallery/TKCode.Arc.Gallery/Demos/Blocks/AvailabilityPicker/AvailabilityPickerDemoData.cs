using TKCode.Arc;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Invented sample data for the Availability picker demos: a 30-minute check-in for three people across one working week
/// (Monday 9 to Friday 13 March 2026), four proposed times a day, one or two of them clashing for somebody.
/// </summary>
internal static class AvailabilityPickerDemoData
{
    public sealed record Week(ProjectPerson[] People, AvailabilityDay[] Days);

    public static Week Create()
    {
        var noor = new ProjectPerson("Noor Haddad", "noor") { Hue = 24 };
        var tomas = new ProjectPerson("Tomás Ferreira", "tomas") { Hue = 210 };
        var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid") { Hue = 140 };
        var monday = new DateOnly(2026, 3, 9);
        AvailabilityDay Day(int offset, params AvailabilitySlot[] slots) => new(monday.AddDays(offset), slots);
        AvailabilitySlot At(int hour, int minute, params ProjectPerson[] busy) => new(new TimeSpan(hour, minute, 0), busy);
        var days = new[]
        {
            Day(0, At(9, 0), At(11, 0), At(13, 30, tomas), At(15, 30)),
            Day(1, At(8, 30, ingrid), At(10, 0), At(14, 0), At(16, 0, noor)),
            Day(2, At(9, 30), At(12, 0, tomas), At(13, 0), At(15, 0)),
            Day(3, At(10, 30), At(11, 30, noor), At(14, 30), At(16, 30, ingrid)),
            Day(4, At(8, 30), At(10, 0), At(13, 0, tomas), At(15, 0)),
        };
        return new Week([noor, tomas, ingrid], days);
    }

    /// <summary>
    /// Harder data: a day where every time clashes for somebody, a time two people are busy for, a day with only two times
    /// (the list changes height), a day with nothing proposed and a time that is already taken.
    /// </summary>
    public static Week CreateTricky()
    {
        var noor = new ProjectPerson("Noor Haddad", "noor") { Hue = 24 };
        var tomas = new ProjectPerson("Tomás Ferreira", "tomas") { Hue = 210 };
        var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid") { Hue = 140 };
        var monday = new DateOnly(2026, 3, 16);
        AvailabilityDay Day(int offset, params AvailabilitySlot[] slots) => new(monday.AddDays(offset), slots);
        AvailabilitySlot At(int hour, int minute, params ProjectPerson[] busy) => new(new TimeSpan(hour, minute, 0), busy);
        var taken = At(11, 30);
        taken.IsEnabled = false;
        taken.Note = "Room already booked";
        var days = new[]
        {
            Day(0, At(9, 0, noor), At(10, 30, tomas), At(13, 0, ingrid), At(16, 0, tomas)),
            Day(1, At(9, 30), At(12, 0, noor, ingrid), At(14, 30), At(17, 0, noor, tomas, ingrid)),
            Day(2, At(10, 0), At(15, 30, tomas)),
            Day(3),
            Day(4, At(9, 0), taken, At(14, 0), At(15, 30, ingrid)),
        };
        return new Week([noor, tomas, ingrid], days);
    }
}
