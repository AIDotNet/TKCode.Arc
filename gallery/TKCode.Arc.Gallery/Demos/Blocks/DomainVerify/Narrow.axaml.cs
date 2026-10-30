using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DomainVerifyNarrowDemo : UserControl
{
    public DomainVerifyNarrowDemo()
    {
        InitializeComponent();

        Block.GetRecordsAction = async (domain, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            return DomainVerifyDemoData.WebsiteRecords(domain);
        };

        // The CAA record holds another authority's value, so the stacked row shows what was found and the fix.
        Block.VerifyAction = async (domain, records, context, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            var checks = records
                .Select(r => r.Id == "caa" && context.Run == 1
                    ? new DomainRecordCheck(r.Id, DomainRecordStatus.Invalid,
                        Found: "0 issue \"other-ca.example\"",
                        Fix: "Replace the CAA value with the one above so the certificate can be issued.")
                    : new DomainRecordCheck(r.Id, DomainRecordStatus.Valid))
                .ToList();
            return new DomainVerifyResult(checks, new DomainRegions(6, 6));
        };
    }
}
