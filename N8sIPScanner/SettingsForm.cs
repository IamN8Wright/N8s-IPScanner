using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace N8sIPScanner;

public sealed class SettingsForm : Form
{
    private readonly RadioButton _darkModeRadio = new();
    private readonly RadioButton _lightModeRadio = new();
    private readonly CheckBox _showLoopbackCheckBox = new();
    private readonly CheckBox _showDisconnectedCheckBox = new();
    private readonly Button _updateOuiButton = new();
    private readonly Label _ouiStatusLabel = new();
    private readonly Button _checkUpdatesButton = new();
    private readonly Label _updateStatusLabel = new();
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();
    private readonly PictureBox _aboutLogoBox = new();
    private readonly Label _aboutTitleLabel = new();
    private readonly Label _aboutTextLabel = new();
    private readonly LinkLabel _aboutEmailLink = new();

    public string SelectedThemeMode => _lightModeRadio.Checked ? "Light" : "Dark";
    public bool ShowLoopbackAdapters => _showLoopbackCheckBox.Checked;
    public bool ShowDisconnectedAdapters => _showDisconnectedCheckBox.Checked;

    public SettingsForm(AppSettings settings)
    {
        UiTheme.SetMode(settings.ThemeMode);

        Text = "Settings";
        Icon = TryLoadIcon();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 690);

        BuildInterface(settings);
        ApplyTheme();
    }

    private static Icon? TryLoadIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    private void BuildInterface(AppSettings settings)
    {
        var contentPanel = new Panel
        {
            Location = new Point(24, 18),
            Size = new Size(512, 510),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(contentPanel);

        var title = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Location = new Point(0, 0),
            Size = new Size(480, 34)
        };
        contentPanel.Controls.Add(title);

        var appearanceTitle = new Label
        {
            Text = "Appearance",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(0, 52),
            Size = new Size(480, 28)
        };
        contentPanel.Controls.Add(appearanceTitle);

        _darkModeRadio.Text = "Dark mode";
        _darkModeRadio.Location = new Point(18, 88);
        _darkModeRadio.Size = new Size(200, 28);
        contentPanel.Controls.Add(_darkModeRadio);

        _lightModeRadio.Text = "Light mode";
        _lightModeRadio.Location = new Point(18, 120);
        _lightModeRadio.Size = new Size(200, 28);
        contentPanel.Controls.Add(_lightModeRadio);

        if (string.Equals(settings.ThemeMode, "Light", StringComparison.OrdinalIgnoreCase))
        {
            _lightModeRadio.Checked = true;
        }
        else
        {
            _darkModeRadio.Checked = true;
        }

        var adaptersTitle = new Label
        {
            Text = "Adapters",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(0, 166),
            Size = new Size(480, 28)
        };
        contentPanel.Controls.Add(adaptersTitle);

        _showLoopbackCheckBox.Text = "Show loopback adapter";
        _showLoopbackCheckBox.Location = new Point(18, 202);
        _showLoopbackCheckBox.Size = new Size(340, 28);
        _showLoopbackCheckBox.Checked = settings.ShowLoopbackAdapters;
        contentPanel.Controls.Add(_showLoopbackCheckBox);

        var loopbackHint = new Label
        {
            Text = "Loopback is only useful for testing 127.0.0.1.",
            Location = new Point(42, 230),
            Size = new Size(430, 24)
        };
        contentPanel.Controls.Add(loopbackHint);

        _showDisconnectedCheckBox.Text = "Show disconnected adapters";
        _showDisconnectedCheckBox.Location = new Point(18, 264);
        _showDisconnectedCheckBox.Size = new Size(340, 28);
        _showDisconnectedCheckBox.Checked = settings.ShowDisconnectedAdapters;
        contentPanel.Controls.Add(_showDisconnectedCheckBox);

        var disconnectedHint = new Label
        {
            Text = "Connected APIPA/no-DHCP adapters remain visible.",
            Location = new Point(42, 292),
            Size = new Size(430, 24)
        };
        contentPanel.Controls.Add(disconnectedHint);

        var ouiTitle = new Label
        {
            Text = "MAC Manufacturer Database",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(0, 338),
            Size = new Size(480, 28)
        };
        contentPanel.Controls.Add(ouiTitle);

        _updateOuiButton.Text = "Update OUI List";
        _updateOuiButton.Location = new Point(18, 374);
        _updateOuiButton.Size = new Size(140, 32);
        _updateOuiButton.Click += async (_, _) => await UpdateOuiAsync();
        contentPanel.Controls.Add(_updateOuiButton);

        _ouiStatusLabel.Text = OuiLookupService.GetDatabaseStatus();
        _ouiStatusLabel.Location = new Point(172, 377);
        _ouiStatusLabel.Size = new Size(320, 44);
        contentPanel.Controls.Add(_ouiStatusLabel);

        var updatesTitle = new Label
        {
            Text = "Software Updates",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(0, 432),
            Size = new Size(480, 28)
        };
        contentPanel.Controls.Add(updatesTitle);

        _checkUpdatesButton.Text = "Check GitHub";
        _checkUpdatesButton.Location = new Point(18, 468);
        _checkUpdatesButton.Size = new Size(140, 32);
        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        contentPanel.Controls.Add(_checkUpdatesButton);

        _updateStatusLabel.Text = "Updates use GitHub releases from IamN8Wright/N8s-IPScanner.";
        _updateStatusLabel.Location = new Point(172, 471);
        _updateStatusLabel.Size = new Size(320, 44);
        contentPanel.Controls.Add(_updateStatusLabel);

        _saveButton.Text = "Save";
        _saveButton.Location = new Point(370, 552);
        _saveButton.Size = new Size(78, 32);
        _saveButton.DialogResult = DialogResult.OK;
        Controls.Add(_saveButton);

        _cancelButton.Text = "Cancel";
        _cancelButton.Location = new Point(458, 552);
        _cancelButton.Size = new Size(78, 32);
        _cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(_cancelButton);

        _aboutLogoBox.Location = new Point(28, 604);
        _aboutLogoBox.Size = new Size(72, 48);
        _aboutLogoBox.SizeMode = PictureBoxSizeMode.Zoom;
        _aboutLogoBox.Image = LoadAboutLogo();
        Controls.Add(_aboutLogoBox);

        _aboutTitleLabel.Text = "InN8 Labs";
        _aboutTitleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _aboutTitleLabel.Location = new Point(112, 604);
        _aboutTitleLabel.Size = new Size(390, 20);
        Controls.Add(_aboutTitleLabel);

        _aboutTextLabel.Text = "N8s IP Scanner is built by InN8 Labs for authorized AV network discovery.";
        _aboutTextLabel.Location = new Point(112, 626);
        _aboutTextLabel.Size = new Size(390, 20);
        Controls.Add(_aboutTextLabel);

        _aboutEmailLink.Text = "iamn8wright@gmail.com";
        _aboutEmailLink.Location = new Point(112, 648);
        _aboutEmailLink.Size = new Size(240, 22);
        _aboutEmailLink.LinkClicked += (_, _) => OpenSupportEmail();
        Controls.Add(_aboutEmailLink);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }


    private static Image? LoadAboutLogo()
    {
        try
        {
            var assembly = typeof(SettingsForm).Assembly;

            using var stream = assembly.GetManifestResourceStream("N8sIPScanner.logo.png");
            if (stream is not null)
            {
                using var embeddedImage = Image.FromStream(stream);
                return new Bitmap(embeddedImage);
            }

            var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
            if (File.Exists(logoPath))
            {
                using var source = Image.FromFile(logoPath);
                return new Bitmap(source);
            }
        }
        catch
        {
            // Settings can still open without the brand mark.
        }

        return null;
    }

    private static void OpenSupportEmail()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:iamn8wright@gmail.com?subject=N8s%20IP%20Scanner",
                UseShellExecute = true
            });
        }
        catch
        {
            Clipboard.SetText("iamn8wright@gmail.com");
            MessageBox.Show(
                "The support email address was copied to your clipboard.",
                "InN8 Labs",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task UpdateOuiAsync()
    {
        var confirm = MessageBox.Show(
            "Download the current IEEE OUI public listing? This helps identify manufacturers from MAC addresses.\n\nAn internet connection is required.",
            "Update OUI List",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _updateOuiButton.Enabled = false;
            _ouiStatusLabel.Text = "Updating OUI list from IEEE...";

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var count = await OuiLookupService.UpdateFromIeeeAsync(timeout.Token);

            _ouiStatusLabel.Text = $"OUI database: {count:N0} entries loaded.";
        }
        catch (OperationCanceledException)
        {
            _ouiStatusLabel.Text = "OUI update canceled or timed out.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update the OUI list.\n\n{ex.Message}",
                "OUI Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _ouiStatusLabel.Text = "OUI update failed. Starter list remains available.";
        }
        finally
        {
            _updateOuiButton.Enabled = true;
        }
    }


    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _checkUpdatesButton.Enabled = false;
            _updateStatusLabel.Text = "Checking GitHub releases...";

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await GitHubUpdateService.CheckForUpdatesAsync(timeout.Token);

            _updateStatusLabel.Text = result.StatusText;

            var buttons = result.IsUpdateAvailable ? MessageBoxButtons.YesNo : MessageBoxButtons.OK;
            var message = result.IsUpdateAvailable
                ? result.Message + "\n\nOpen the GitHub release page now?"
                : result.Message;

            var choice = MessageBox.Show(
                message,
                "Software Updates",
                buttons,
                MessageBoxIcon.Information);

            if (result.IsUpdateAvailable && choice == DialogResult.Yes)
            {
                GitHubUpdateService.OpenReleasePage(result.ReleaseUrl);
            }
        }
        catch (OperationCanceledException)
        {
            _updateStatusLabel.Text = "GitHub update check timed out.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not check GitHub for updates.\n\n{ex.Message}",
                "Software Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _updateStatusLabel.Text = "GitHub update check failed.";
        }
        finally
        {
            _checkUpdatesButton.Enabled = true;
        }
    }

    private void ApplyTheme()
    {
        UiTheme.Apply(this);
        UiTheme.StyleSecondary(_cancelButton);
        _aboutEmailLink.LinkColor = UiTheme.Accent;
        _aboutEmailLink.ActiveLinkColor = UiTheme.Accent;
        _aboutEmailLink.VisitedLinkColor = UiTheme.Accent;

        foreach (Control control in Controls)
        {
            if (control is Panel panel)
            {
                panel.BackColor = UiTheme.Background;
            }
        }

        foreach (Control control in Controls)
        {
            ApplyLabelHintColor(control);
        }
    }

    private static void ApplyLabelHintColor(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label label &&
                label.Font.Size < 12 &&
                !label.Font.Bold)
            {
                label.ForeColor = UiTheme.MutedText;
            }

            if (child.HasChildren)
            {
                ApplyLabelHintColor(child);
            }
        }
    }
}
