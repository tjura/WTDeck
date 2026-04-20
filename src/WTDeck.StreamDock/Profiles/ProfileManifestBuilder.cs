using WTDeck.StreamDock.Configuration;

namespace WTDeck.StreamDock.Profiles;

public sealed class ProfileManifestBuilder
{
    private const string UuidNamespace = "wtdeck";
    private const string ProfileUuidNamespace = "wtdeck-profile";
    private const string PageUuidNamespace = "wtdeck-page";

    /// <summary>
    /// Builds the WTDeck profile manifest with a single landing gear button at (0,0).
    /// </summary>
    public (string ProfileUuid, string PageUuid, ProfileManifest Manifest) Build(StreamDockOptions options)
    {
        var profileUuid = DeterministicUuid.Create(ProfileUuidNamespace, options.ProfileName).ToString("D").ToUpperInvariant();
        var pageUuid = DeterministicUuid.Create(PageUuidNamespace, options.ProfileName + ":page0").ToString("D").ToUpperInvariant();
        var gearActionId = DeterministicUuid.Create(UuidNamespace, options.ProfileName + ":landing-gear:0,0").ToString("D").ToUpperInvariant();

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
                }
            }
        };

        return (profileUuid, pageUuid, manifest);
    }
}
