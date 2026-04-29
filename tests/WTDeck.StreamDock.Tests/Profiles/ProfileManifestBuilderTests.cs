using FluentAssertions;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Profiles;

namespace WTDeck.StreamDock.Tests.Profiles;

public class ProfileManifestBuilderTests
{
    private readonly ProfileManifestBuilder _builder = new();
    private readonly StreamDockOptions _options = new()
    {
        ProfileName = "WTDeck",
        DeviceUUID = "CN001V3Device",
        DeviceSerialNumber = "8730DB78224F",
        DeviceModel = "20GBA9901",
        PluginActionUuid = "com.wtdeck.streamdock.gear",
        PluginFlaresActionUuid = "com.wtdeck.streamdock.flares",
        PluginFlightAlertsActionUuid = "com.wtdeck.streamdock.flight-alerts",
        FlightAlertsPanelSlot = "5,0"
    };

    [Fact]
    public void Builds_profile_with_correct_device_metadata()
    {
        var (_, _, manifest) = _builder.Build(_options);

        manifest.Name.Should().Be("WTDeck");
        manifest.DeviceUUID.Should().Be("CN001V3Device");
        manifest.DeviceSerialNumber.Should().Be("8730DB78224F");
        manifest.DeviceModel.Should().Be("20GBA9901");
        manifest.AppIdentifier.Should().Be("*");
    }

    [Fact]
    public void Profile_has_landing_gear_button_at_0_0()
    {
        var (_, _, manifest) = _builder.Build(_options);

        manifest.Actions.Should().ContainKey("0,0");
        var action = manifest.Actions["0,0"];
        action.UUID.Should().Be("com.wtdeck.streamdock.gear");
        action.Name.Should().Be("Landing Gear");
        action.States.Should().HaveCount(1);
    }

    [Fact]
    public void Profile_has_launch_flares_button_at_0_4()
    {
        var (_, _, manifest) = _builder.Build(_options);

        manifest.Actions.Should().ContainKey("0,4");
        var action = manifest.Actions["0,4"];
        action.UUID.Should().Be("com.wtdeck.streamdock.flares");
        action.Name.Should().Be("Launch Flares");
        action.States.Should().HaveCount(1);
        action.States[0].Image.Should().Be("assets/flare-unknown");
        action.States[0].ShowTitle.Should().BeTrue();
    }

    [Fact]
    public void Profile_has_flight_alerts_information_panel()
    {
        var (_, _, manifest) = _builder.Build(_options);

        manifest.Actions.Should().ContainKey("5,0");
        var action = manifest.Actions["5,0"];
        action.UUID.Should().Be("com.wtdeck.streamdock.flight-alerts");
        action.Name.Should().Be("Over-G Alert");
        action.Controller.Should().Be("Information");
        action.States.Should().HaveCount(1);
        action.States[0].Image.Should().Be("assets/flight-alerts-panel");
        action.States[0].ShowTitle.Should().BeFalse();
    }

    [Fact]
    public void Profile_and_action_ids_are_deterministic()
    {
        var (p1, page1, m1) = _builder.Build(_options);
        var (p2, page2, m2) = _builder.Build(_options);

        p1.Should().Be(p2);
        page1.Should().Be(page2);
        m1.Actions["0,0"].ActionID.Should().Be(m2.Actions["0,0"].ActionID);
        m1.Actions["0,4"].ActionID.Should().Be(m2.Actions["0,4"].ActionID);
        m1.Actions["5,0"].ActionID.Should().Be(m2.Actions["5,0"].ActionID);
    }

    [Fact]
    public void Profile_has_single_page_listed_as_current()
    {
        var (_, pageUuid, manifest) = _builder.Build(_options);
        manifest.Pages.Pages.Should().HaveCount(1);
        manifest.Pages.Current.Should().Be($"{pageUuid}.sdProfile");
    }
}
