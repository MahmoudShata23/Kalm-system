using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;

namespace Kalm.UnitTests.Organization;

public sealed class OrganizationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNamesAndStartsInSetup()
    {
        var organization = Kalm.Organization.Domain.Organization.Create(Guid.NewGuid(), new OrganizationName("  Kalm  ", 120), "  Kalm Specialty Coffee  ", new CurrencyCode("egp"), new LocaleCode("en"), Now);

        Assert.Equal("Kalm", organization.BrandName);
        Assert.Equal("Kalm Specialty Coffee", organization.LegalName);
        Assert.Equal("EGP", organization.DefaultCurrencyCode);
        Assert.Equal(OrganizationStatus.Setup, organization.Status);
        Assert.Equal(1, organization.Version);
    }

    [Fact]
    public void Update_AdvancesVersion()
    {
        var organization = Kalm.Organization.Domain.Organization.Create(Guid.NewGuid(), new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), Now);

        organization.Update(new OrganizationName("Kalm Coffee", 120), null, new CurrencyCode("EGP"), new LocaleCode("ar-EG"), Now.AddMinutes(1));

        Assert.Equal(2, organization.Version);
        Assert.Equal("ar-EG", organization.DefaultLocaleCode);
    }

    [Fact]
    public void ArchivedOrganization_CannotBeReactivated()
    {
        var organization = Kalm.Organization.Domain.Organization.Create(Guid.NewGuid(), new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), Now);
        organization.ChangeStatus(OrganizationStatus.Archived, Now);

        Assert.Throws<InvalidOperationException>(() => organization.ChangeStatus(OrganizationStatus.Active, Now));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("cairo-01")]
    public void BranchCode_NormalizesToUppercase(string code)
    {
        Assert.Equal(code.ToUpperInvariant(), new BranchCode(code).Value);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("CAIRO_01")]
    [InlineData("Cairo 01")]
    public void BranchCode_RejectsInvalidValues(string code)
    {
        Assert.Throws<ArgumentException>(() => new BranchCode(code));
    }

    [Fact]
    public void Validation_RejectsInvalidCurrencyLocaleAndTimeZone()
    {
        Assert.Throws<ArgumentException>(() => new CurrencyCode("EG"));
        Assert.Throws<ArgumentException>(() => new LocaleCode("fr"));
        Assert.Throws<ArgumentException>(() => new TimeZoneId("not/a-time-zone"));
    }

    [Fact]
    public void BusinessDayRollover_UsesMinutePrecisionAndRoundTrips()
    {
        var rollover = BusinessDayRollover.Parse("04:00");

        Assert.Equal("04:00", rollover.ToString());
        Assert.Throws<ArgumentException>(() => new BusinessDayRollover(new TimeOnly(4, 0, 1)));
        Assert.Throws<ArgumentException>(() => BusinessDayRollover.Parse("04:00:00"));
    }

    [Fact]
    public void BranchLifecycle_AdvancesVersion()
    {
        var branch = Branch.Create(Guid.NewGuid(), Guid.NewGuid(), new OrganizationName("Cairo", 120), new BranchCode("CAI-01"), new LocaleCode("ar-EG"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), Now);

        branch.ChangeStatus(BranchStatus.Active, Now.AddMinutes(1));

        Assert.Equal(2, branch.Version);
        Assert.Equal(BranchStatus.Active, branch.Status);
    }

    [Fact]
    public void BranchUpdate_NoOpKeepsVersionAndLifecycleIsExplicit()
    {
        var branch = Branch.Create(Guid.NewGuid(), Guid.NewGuid(), new OrganizationName("Cairo", 120), new BranchCode("CAI-01"), new LocaleCode("ar-EG"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), Now);

        Assert.False(branch.Update(new OrganizationName("Cairo", 120), new BranchCode("cai-01"), new LocaleCode("ar-EG"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), Now.AddMinutes(1)));
        Assert.Equal(1, branch.Version);
        Assert.True(branch.Activate(Now.AddMinutes(2)));
        Assert.False(branch.Activate(Now.AddMinutes(3)));
        Assert.True(branch.Deactivate(Now.AddMinutes(4)));
        Assert.False(branch.Deactivate(Now.AddMinutes(5)));
        Assert.Equal(3, branch.Version);
    }
}
