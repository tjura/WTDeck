using System.Drawing;
using Microsoft.Extensions.Logging;

namespace WTDeck.App.Tray;

public sealed class TrayIconManager : IDisposable
{
    private readonly ILogger<TrayIconManager> _logger;
    private NotifyIcon? _notifyIcon;
    private readonly Action _onExit;

    public TrayIconManager(ILogger<TrayIconManager> logger, Action onExit)
    {
        _logger = logger;
        _onExit = onExit;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "WTDeck - War Thunder Stream Deck",
            Visible = true,
            ContextMenuStrip = CreateMenu()
        };

        // Create a simple green icon programmatically
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(0, 255, 65)); // Green neon
        _notifyIcon.Icon = Icon.FromHandle(bmp.GetHicon());

        _logger.LogInformation("Tray icon initialized");
    }

    public void SetStatus(string status)
    {
        if (_notifyIcon is not null)
            _notifyIcon.Text = $"WTDeck - {status}";
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("WTDeck", null, (_, _) => { });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _onExit());
        return menu;
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
