namespace ScreenTime.Agent.Helper;

/// <summary>
/// Shows the child a notice, always on top: a small window in the corner that closes by itself (a warning), or a full
/// screen over the main display until the child presses OK (a program was closed).
/// </summary>
public sealed class NoticeForm : Form
{
    public static readonly TimeSpan WarningShownFor = TimeSpan.FromSeconds(15);

    private readonly bool _fullScreen;
    private readonly Label _text;
    private readonly System.Windows.Forms.Timer? _autoClose;

    public NoticeForm(bool fullScreen)
    {
        _fullScreen = fullScreen;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 48);
        ForeColor = Color.White;
        Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, fullScreen ? 24 : 12);
        Padding = new Padding(16);

        var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, Height = Font.Height * 2, FlatStyle = FlatStyle.Flat };
        ok.Click += (_, _) => Close();
        _text = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(_text);
        Controls.Add(ok);
        AcceptButton = ok;

        var screen = Screen.PrimaryScreen!;
        if (fullScreen)
        {
            Bounds = screen.Bounds;
            ok.Dock = DockStyle.None;
            ok.Size = new Size(Font.Height * 6, Font.Height * 2);
            ok.Location = new Point((Width - ok.Width) / 2, Height * 2 / 3);
        }
        else
        {
            Size = new Size(Font.Height * 24, Font.Height * 8);
            Location = new Point(screen.WorkingArea.Right - Width - 16, screen.WorkingArea.Bottom - Height - 16);
            _autoClose = new System.Windows.Forms.Timer { Interval = (int)WarningShownFor.TotalMilliseconds };
            _autoClose.Tick += (_, _) => Close();
        }
    }

    /// <summary>A warning shouldn't take the keyboard away from whatever the child is typing in.</summary>
    protected override bool ShowWithoutActivation => !_fullScreen;

    public void ShowText(string text)
    {
        _text.Text = text;
        Show();
        if (_fullScreen)
            Activate();
        _autoClose?.Stop();
        _autoClose?.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _autoClose?.Dispose();
        base.Dispose(disposing);
    }
}
