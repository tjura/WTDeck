using WTDeck.StreamDock.Configuration;

namespace WTDeck.StreamDock.Profiles;

public sealed class ProfileManifestBuilder
{
    private const string UuidNamespace = "wtdeck";
    private const string ProfileUuidNamespace = "wtdeck-profile";
    private const string PageUuidNamespace = "wtdeck-page";

    /// <summary>
    /// Builds the WTDeck profile manifest with landing gear at (0,0), native gear at
    /// (1,0), flares at (0,4), and a read-only over-G alert tile on the device
    /// information controller.
    /// </summary>
    public (string ProfileUuid, string PageUuid, ProfileManifest Manifest) Build(StreamDockOptions options)
    {
        var profileUuid = DeterministicUuid.Create(ProfileUuidNamespace, options.ProfileName).ToString("D").ToUpperInvariant();
        var pageUuid = DeterministicUuid.Create(PageUuidNamespace, options.ProfileName + ":page0").ToString("D").ToUpperInvariant();
        var gearActionId = DeterministicUuid.Create(UuidNamespace, options.ProfileName + ":landing-gear:0,0").ToString("D").ToUpperInvariant();
        var nativeGearActionId = DeterministicUuid.Create(UuidNamespace, options.ProfileName + $":landing-gear-native:{options.NativeGearSlot}").ToString("D").ToUpperInvariant();
        var flaresActionId = DeterministicUuid.Create(UuidNamespace, options.ProfileName + ":launch-flares:0,4").ToString("D").ToUpperInvariant();
        var flightAlertsActionId = DeterministicUuid.Create(UuidNamespace, options.ProfileName + $":flight-alerts:{options.FlightAlertsPanelSlot}").ToString("D").ToUpperInvariant();

        var pageFileName = $"{pageUuid}.sdProfile";

        var manifest = new ProfileManifest
        {
            Name = options.ProfileName,
            Version = "1.0",
            DeviceUUID = options.DeviceUUID,
            DeviceSerialNumber = options.DeviceSerialNumber,
            DeviceModel = options.DeviceModel,
            AppIdentifier = "*",
            Pages = new ProfilePages
            {
                Current = pageFileName,
                Pages = [pageFileName]
            },
            Actions = new Dictionary<string, ProfileAction>
            {
                ["0,0"] = new ProfileAction
                {
                    ActionID = gearActionId,
                    UUID = options.PluginActionUuid,
                    Name = "Landing Gear",
                    State = 0,
                    Settings = [],
                    States =
                    [
                        new ProfileActionState
                        {
                            Image = "assets/gear-disabled",
                            Title = "LANDING\nGEAR",
                            FontSize = "14",
                            TitleAlignment = "middle",
                            ShowTitle = false
                        }
                    ]
                },
                [options.NativeGearSlot] = new ProfileAction
                {
                    ActionID = nativeGearActionId,
                    UUID = options.PluginNativeGearActionUuid,
                    Name = "Landing Gear Native",
                    State = 0,
                    Settings = [],
                    States =
                    [
                        new ProfileActionState
                        {
                            Image = "assets/gear-unknown",
                            Title = "NATIVE\nGEAR",
                            FontSize = "14",
                            TitleAlignment = "middle",
                            ShowTitle = false
                        }
                    ]
                },
                ["0,4"] = new ProfileAction
                {
                    ActionID = flaresActionId,
                    UUID = options.PluginFlaresActionUuid,
                    Name = "Launch Flares",
                    State = 0,
                    Settings = [],
                    States =
                    [
                        new ProfileActionState
                        {
                            Image = "assets/flare-unknown",
                            Title = "FLARES",
                            FontSize = "15",
                            TitleAlignment = "middle",
                            ShowTitle = true
                        }
                    ]
                },
                [options.FlightAlertsPanelSlot] = new ProfileAction
                {
                    ActionID = flightAlertsActionId,
                    Controller = "Information",
                    UUID = options.PluginFlightAlertsActionUuid,
                    Name = "Over-G Alert",
                    State = 0,
                    Settings = [],
                    States =
                    [
                        new ProfileActionState
                        {
                            Image = "assets/flight-alerts-panel",
                            Title = "",
                            FontSize = "12",
                            TitleAlignment = "middle",
                            ShowTitle = false
                        }
                    ]
                }
            }
        };

        return (profileUuid, pageUuid, manifest);
    }
}
