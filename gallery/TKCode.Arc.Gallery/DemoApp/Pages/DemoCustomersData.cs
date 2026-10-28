namespace TKCode.Arc.Gallery.DemoApp;

public enum CustomerPlan
{
    Starter,
    Growth,
    Scale,
    Enterprise
}

public enum CustomerStatus
{
    Active,
    Trial,
    PastDue
}

public enum CustomerHealth
{
    Healthy,
    Attention,
    Risk
}

/// <summary>
/// A customer account on the Customers page. The owner and the trial length change from the row menu, so they are
/// settable; the rest is fixed.
/// </summary>
public sealed class DemoCustomer(string id, string name, string domain, CustomerPlan plan, CustomerStatus status, CustomerHealth health,
                                 int mrr, int seats, string owner, double seenHours, int trialDays)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Domain { get; } = domain;
    public CustomerPlan Plan { get; } = plan;
    public CustomerStatus Status { get; } = status;
    public CustomerHealth Health { get; } = health;
    public int Mrr { get; } = mrr;
    public int Seats { get; } = seats;
    public string Owner { get; set; } = owner;
    public double SeenHours { get; } = seenHours;
    public int TrialDays { get; set; } = trialDays;
}

public static partial class DemoData
{
    /// <summary>The account owners on the Customers page, in uiarc.dev's menu order.</summary>
    public static IReadOnlyList<string> CustomerOwners { get; } = ["emma", "ava", "marcus", "jasmine", "sofia"];

    /// <summary>
    /// uiarc.dev's 62 customer accounts (mulberry32, seed 1990), with the exact hours since each was last seen. A fresh
    /// copy each call, because the page reassigns owners and extends trials.
    /// </summary>
    public static List<DemoCustomer> Customers() =>
    [
        new("c1", "Halcyon Freight", "halcyonfreight.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 180, 15, "ava", 7.49, 0),
        new("c2", "Brightline Health", "brightlinehealth.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 60, 5, "jasmine", 13.49, 0),
        new("c3", "Kestrel Analytics", "kestrel.io", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 190, 16, "jasmine", 17.46, 0),
        new("c4", "Juniper Labs", "juniperlabs.dev", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Attention, 130, 11, "emma", 98, 0),
        new("c5", "Orbital Payroll", "orbitalpayroll.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 13540, 467, "jasmine", 3.29, 0),
        new("c6", "Tidewater Credit Union", "tidewatercu.org", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Attention, 100, 8, "marcus", 101, 0),
        new("c7", "Fernhill Studio", "fernhill.studio", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1180, 49, "emma", 20.26, 0),
        new("c8", "Copperleaf Insurance", "copperleaf.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 17550, 605, "sofia", 4.63, 0),
        new("c9", "Wavelength Media", "wavelength.example", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 840, 35, "emma", 18.19, 0),
        new("c10", "Summit Clinics", "summitclinics.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 60, 5, "marcus", 11.16, 0),
        new("c11", "Parallax Robotics", "parallaxrobotics.ai", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Attention, 3340, 88, "sofia", 95, 0),
        new("c12", "Bluebird Dental", "bluebirddental.example", CustomerPlan.Growth, CustomerStatus.PastDue, CustomerHealth.Risk, 790, 33, "sofia", 863, 0),
        new("c13", "Lattice Energy", "latticeenergy.com", CustomerPlan.Starter, CustomerStatus.PastDue, CustomerHealth.Risk, 220, 18, "ava", 361, 0),
        new("c14", "Meadowlark Foods", "meadowlarkfoods.com", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 120, 10, "emma", 18.19, 0),
        new("c15", "Quarry Legal", "quarrylegal.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 100, 8, "marcus", 23.5, 0),
        new("c16", "Redwood Mobility", "redwoodmobility.example", CustomerPlan.Starter, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 12, "jasmine", 2.06, 12),
        new("c17", "Silverline Travel", "silverline.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 5810, 153, "emma", 25.63, 0),
        new("c18", "Anchor Learning", "anchorlearning.example", CustomerPlan.Starter, CustomerStatus.Trial, CustomerHealth.Risk, 0, 10, "sofia", 392, 7),
        new("c19", "Beacon Outfitters", "beaconoutfitters.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Attention, 80, 7, "ava", 69, 0),
        new("c20", "Cobalt Security", "cobaltsec.io", CustomerPlan.Growth, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 51, "jasmine", 1.2, 4),
        new("c21", "Driftwood Hotels", "driftwoodhotels.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 130, 11, "ava", 14.22, 0),
        new("c22", "Evergreen Lending", "evergreenlending.example", CustomerPlan.Scale, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 124, "sofia", 17.8, 3),
        new("c23", "Fathom Marine", "fathommarine.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Attention, 2660, 70, "sofia", 210, 0),
        new("c24", "Granite Works", "graniteworks.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 5320, 140, "marcus", 25.29, 0),
        new("c25", "Hollow Oak Games", "hollowoak.games", CustomerPlan.Growth, CustomerStatus.PastDue, CustomerHealth.Risk, 480, 20, "ava", 756, 0),
        new("c26", "Ironbark Construction", "ironbark.build", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 550, 23, "emma", 9.26, 0),
        new("c27", "Jetty Payments", "jetty.money", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Attention, 100, 8, "ava", 202, 0),
        new("c28", "Kiln Ceramics", "kilnceramics.com", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 3840, 101, "ava", 25.03, 0),
        new("c29", "Lumen Optics", "lumenoptics.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 80, 7, "jasmine", 23.69, 0),
        new("c30", "Marlow & Finch", "marlowfinch.example", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1270, 53, "marcus", 6.09, 0),
        new("c31", "Northstar Realty", "northstarrealty.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 2960, 78, "marcus", 3.28, 0),
        new("c32", "Oakridge Veterinary", "oakridgevet.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 15490, 534, "ava", 18.13, 0),
        new("c33", "Pinecrest Schools", "pinecrestschools.org", CustomerPlan.Growth, CustomerStatus.PastDue, CustomerHealth.Risk, 1200, 50, "ava", 940, 0),
        new("c34", "Quill Publishing", "quillpublishing.example", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Risk, 620, 26, "marcus", 829, 0),
        new("c35", "Riverbend Coffee", "riverbendcoffee.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 16530, 570, "marcus", 15.26, 0),
        new("c36", "Saltmarsh Brewing", "saltmarsh.beer", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1030, 43, "sofia", 21.12, 0),
        new("c37", "Tessellate Design", "tessellate.design", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 2510, 66, "ava", 1.05, 0),
        new("c38", "Umbra Solar", "umbrasolar.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Attention, 180, 15, "ava", 126, 0),
        new("c39", "Vantage Recruiting", "vantagerecruiting.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 50, 4, "jasmine", 25.01, 0),
        new("c40", "Willow Care", "willowcare.health", CustomerPlan.Starter, CustomerStatus.Trial, CustomerHealth.Attention, 0, 8, "emma", 186, 8),
        new("c41", "Yardline Sports", "yardline.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Attention, 15230, 525, "jasmine", 207, 0),
        new("c42", "Zephyr Air", "zephyrair.com", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1100, 46, "jasmine", 3.75, 0),
        new("c43", "Arbor Pediatrics", "arborpeds.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Attention, 13690, 472, "emma", 201, 0),
        new("c44", "Basalt Data", "basaltdata.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 70, 6, "sofia", 14.45, 0),
        new("c45", "Cinder Labs", "cinderlabs.dev", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 5550, 146, "jasmine", 15.25, 0),
        new("c46", "Delta Harbor Shipping", "deltaharbor.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 6870, 237, "emma", 16.59, 0),
        new("c47", "Ember Kitchens", "emberkitchens.example", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Risk, 1340, 56, "emma", 705, 0),
        new("c48", "Foxglove Pharmacy", "foxglove.care", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1200, 50, "jasmine", 16.31, 0),
        new("c49", "Gullwing Aviation", "gullwing.aero", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 5850, 154, "sofia", 0.42, 0),
        new("c50", "Heron Biosciences", "heronbio.example", CustomerPlan.Growth, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 51, "marcus", 0.03, 5),
        new("c51", "Indigo Mortgage", "indigomortgage.example", CustomerPlan.Growth, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 19, "emma", 8.22, 6),
        new("c52", "Juno Fitness", "junofitness.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Risk, 50, 4, "jasmine", 253, 0),
        new("c53", "Keystone Civil", "keystonecivil.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Attention, 3340, 88, "ava", 168, 0),
        new("c54", "Larkspur Florals", "larkspur.flowers", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 620, 26, "marcus", 22.01, 0),
        new("c55", "Monarch Accounting", "monarchcpa.example", CustomerPlan.Enterprise, CustomerStatus.Active, CustomerHealth.Healthy, 7370, 254, "marcus", 21.79, 0),
        new("c56", "Nimbus Weather", "nimbusweather.io", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Healthy, 2700, 71, "marcus", 1.78, 0),
        new("c57", "Outpost Supply", "outpostsupply.example", CustomerPlan.Growth, CustomerStatus.Trial, CustomerHealth.Healthy, 0, 27, "ava", 18.05, 7),
        new("c58", "Prairie Mutual", "prairiemutual.com", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Risk, 910, 38, "emma", 266, 0),
        new("c59", "Ridgeway Transit", "ridgewaytransit.example", CustomerPlan.Starter, CustomerStatus.Active, CustomerHealth.Healthy, 80, 7, "jasmine", 21.76, 0),
        new("c60", "Sable Audio", "sableaudio.example", CustomerPlan.Growth, CustomerStatus.Active, CustomerHealth.Healthy, 1300, 54, "sofia", 14.89, 0),
        new("c61", "Thistle Bakery", "thistlebakery.example", CustomerPlan.Scale, CustomerStatus.Active, CustomerHealth.Attention, 3690, 97, "emma", 152, 0),
        new("c62", "Upland Farms", "uplandfarms.example", CustomerPlan.Growth, CustomerStatus.Trial, CustomerHealth.Risk, 0, 30, "marcus", 470, 10)
    ];

    public static string Label(CustomerPlan plan) => plan switch
    {
        CustomerPlan.Starter => "Starter",
        CustomerPlan.Growth => "Growth",
        CustomerPlan.Scale => "Scale",
        _ => "Enterprise"
    };

    public static string Label(CustomerStatus status) => status switch
    {
        CustomerStatus.Active => "Active",
        CustomerStatus.Trial => "Trial",
        _ => "Past due"
    };

    public static string Label(CustomerHealth health) => health switch
    {
        CustomerHealth.Healthy => "Healthy",
        CustomerHealth.Attention => "Needs attention",
        _ => "At risk"
    };
}
