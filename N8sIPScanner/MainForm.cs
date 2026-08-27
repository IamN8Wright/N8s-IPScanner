using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace N8sIPScanner;

public sealed class MainForm : Form
{
    private readonly PictureBox _logoBox = new();
    private readonly Label _titleLabel = new();
    private readonly GroupBox _interfaceGroup = new();
    private readonly ComboBox _interfaceCombo = new();
    private readonly Button _refreshInterfacesButton = new();
    private readonly ListView _interfaceView = new();
    private readonly GroupBox _scanGroup = new();
    private readonly TextBox _subnetBox = new();
    private readonly TextBox _startBox = new();
    private readonly TextBox _endBox = new();
    private readonly CheckBox _fullSubnetCheckBox = new();
    private readonly TextBox _timeoutBox = new();
    private readonly Button _scanButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _discoverSubnetButton = new();
    private readonly GroupBox _resultsGroup = new();

    private readonly ListView _resultsView = new();
    private readonly GroupBox _nicSettingsGroup = new();
    private readonly TextBox _selectedNicBox = new();
    private readonly TextBox _ipAddressBox = new();
    private readonly TextBox _subnetMaskBox = new();
    private readonly TextBox _gatewayBox = new();
    private readonly TextBox _primaryDnsBox = new();
    private readonly TextBox _secondaryDnsBox = new();
    private readonly Button _applyStaticButton = new();
    private readonly Button _setDhcpButton = new();
    private readonly AnimatedCobaltProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly Button _exportButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _updateOuiButton = new();
    private readonly Button _settingsButton = new SettingsGearButton();
    private readonly ToolTip _toolTip = new();

    private readonly List<NetworkInterfaceInfo> _interfaces = new();
    private readonly List<ScanResult> _results = new();
    private readonly IpScanner _scanner = new();

    private CancellationTokenSource? _scanCancellation;
    private string _lastScanDescription = "";

    public MainForm()
    {
        AppSettingsService.Load();
        UiTheme.SetMode(AppSettingsService.Current.ThemeMode);

        Text = "N8s IP Scanner";
        Icon = TryLoadIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 860);
        Size = new Size(1320, 900);

        BuildInterface();
        WireEvents();

        Resize += (_, _) =>
        {
            StyleSettingsGear();
            CenterNicSettingsButtons();
        };

        Shown += (_, _) =>
        {
            RefreshInterfaces();
            SetStatus("Ready. " + OuiLookupService.GetDatabaseStatus());
        };
    }

    private static Icon? TryLoadIcon()
    {
        try
        {
            var executablePath = Application.ExecutablePath;
            var extracted = Icon.ExtractAssociatedIcon(executablePath);
            if (extracted is not null)
            {
                return extracted;
            }
        }
        catch
        {
            // Fall back to app.ico if running from source.
        }

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            return File.Exists(iconPath) ? new Icon(iconPath) : null;
        }
        catch
        {
            return null;
        }
    }

    private void BuildInterface()
    {
        SuspendLayout();

        _logoBox.Location = new Point(14, 10);
        _logoBox.Size = new Size(112, 62);
        _logoBox.SizeMode = PictureBoxSizeMode.Zoom;
        _logoBox.BorderStyle = BorderStyle.None;
        _logoBox.Image = LoadLogo();
        Controls.Add(_logoBox);

        _titleLabel.Text = "N8s IP Scanner";
        _titleLabel.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        _titleLabel.AutoSize = true;
        _titleLabel.Location = new Point(140, 14);
        _titleLabel.ForeColor = UiTheme.Text;
        Controls.Add(_titleLabel);

        _settingsButton.Text = "";
        _settingsButton.Location = new Point(ClientSize.Width - 82, 20);
        _settingsButton.Size = new Size(32, 32);
        _settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _settingsButton.TabStop = false;
        _settingsButton.FlatStyle = FlatStyle.Flat;
        _settingsButton.FlatAppearance.BorderSize = 0;
        _settingsButton.BackColor = UiTheme.Background;
        _settingsButton.ForeColor = UiTheme.MutedText;
        _settingsButton.UseVisualStyleBackColor = false;
        Controls.Add(_settingsButton);
        _toolTip.SetToolTip(_settingsButton, "Settings");

        BuildInterfaceGroup();
        BuildScanGroup();
        BuildResultsGrid();
        BuildNetworkSettingsPanel();
        BuildBottomControls();

        ResumeLayout(false);
        UiTheme.Apply(this);
        UiTheme.StyleSecondary(_refreshInterfacesButton);
        UiTheme.StyleSecondary(_stopButton);
        UiTheme.StyleSecondary(_clearButton);
        UiTheme.StyleSecondary(_setDhcpButton);
        ApplyHeadingFonts();
        StyleSettingsGear();
        _interfaceGroup.Resize += (_, _) =>
        {
            AlignInterfaceHeaderControls();
            StyleSettingsGear();
        };
        _nicSettingsGroup.Resize += (_, _) => CenterNicSettingsButtons();
        AlignInterfaceHeaderControls();
        PerformLayout();
    }

    private static Image? LoadLogo()
    {
        try
        {
            var assembly = typeof(MainForm).Assembly;

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
            // App can still run without the logo.
        }

        return null;
    }

    private void BuildInterfaceGroup()
    {
        _interfaceGroup.Text = "Local Interfaces";
        _interfaceGroup.Location = new Point(20, 78);
        _interfaceGroup.Size = new Size(1250, 190);
        _interfaceGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_interfaceGroup);

        var interfaceLabel = new Label
        {
            Text = "Use interface:",
            Location = new Point(12, 28),
            Size = new Size(85, 24)
        };
        _interfaceGroup.Controls.Add(interfaceLabel);

        _interfaceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _interfaceCombo.Location = new Point(100, 25);
        _interfaceCombo.Size = new Size(1015, 24);
        _interfaceCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _interfaceGroup.Controls.Add(_interfaceCombo);

        _refreshInterfacesButton.Text = "Refresh";
        _refreshInterfacesButton.Location = new Point(1145, 23);
        _refreshInterfacesButton.Size = new Size(90, 28);
        _refreshInterfacesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _interfaceGroup.Controls.Add(_refreshInterfacesButton);

        _interfaceView.Location = new Point(15, 62);
        _interfaceView.Size = new Size(1220, 112);
        _interfaceView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _interfaceView.View = View.Details;
        _interfaceView.FullRowSelect = true;
        _interfaceView.GridLines = false;
        _interfaceView.HideSelection = false;
        _interfaceView.Columns.Add("Interface", 170);
        _interfaceView.Columns.Add("IPv4", 115);
        _interfaceView.Columns.Add("Subnet Mask", 110);
        _interfaceView.Columns.Add("DHCP/Static", 120);
        _interfaceView.Columns.Add("Link", 80);
        _interfaceView.Columns.Add("Type", 120);
        _interfaceView.Columns.Add("Gateway", 140);
        _interfaceView.Columns.Add("DNS", 170);
        _interfaceView.Columns.Add("MAC", 150);
        _interfaceGroup.Controls.Add(_interfaceView);
    }

    private void StyleSettingsGear()
    {
        const int gearSize = 32;

        _settingsButton.Text = "";
        _settingsButton.Size = new Size(gearSize, gearSize);

        var titleHeight = Math.Max(_titleLabel.Height, 38);
        var titleCenterY = _titleLabel.Top + (titleHeight / 2);
        _settingsButton.Top = Math.Max(0, titleCenterY - (gearSize / 2));

        // Align the gear to the same right edge as the main panes.
        var rightEdge = _interfaceGroup.Right > 0 ? _interfaceGroup.Right : ClientSize.Width - 50;
        _settingsButton.Left = rightEdge - _settingsButton.Width;

        _settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _settingsButton.TabStop = false;
        _settingsButton.FlatStyle = FlatStyle.Flat;
        _settingsButton.FlatAppearance.BorderSize = 0;
        _settingsButton.BackColor = UiTheme.Background;
        _settingsButton.ForeColor = UiTheme.MutedText;
        _settingsButton.UseVisualStyleBackColor = false;
        _settingsButton.BringToFront();
        _settingsButton.Invalidate();
    }

    private void ApplyHeadingFonts()
    {
        var headingFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        var normalFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        _interfaceGroup.Font = headingFont;
        _scanGroup.Font = headingFont;
        _resultsGroup.Font = headingFont;
        _nicSettingsGroup.Font = headingFont;

        ApplyNormalFontToChildren(_interfaceGroup, normalFont);
        ApplyNormalFontToChildren(_scanGroup, normalFont);
        ApplyNormalFontToChildren(_resultsGroup, normalFont);
        ApplyNormalFontToChildren(_nicSettingsGroup, normalFont);

        _titleLabel.Font = new Font("Segoe UI", 20.5f, FontStyle.Bold);
    }

    private static void ApplyNormalFontToChildren(Control parent, Font normalFont)
    {
        foreach (Control child in parent.Controls)
        {
            child.Font = normalFont;

            if (child.HasChildren)
            {
                ApplyNormalFontToChildren(child, normalFont);
            }
        }
    }

    private void AlignInterfaceHeaderControls()
    {
        if (_interfaceGroup.ClientSize.Width <= 0)
        {
            return;
        }

        const int rightMargin = 15;
        const int gap = 25;

        _refreshInterfacesButton.Left = _interfaceGroup.ClientSize.Width - _refreshInterfacesButton.Width - rightMargin;
        _refreshInterfacesButton.Top = 23;

        _interfaceCombo.Left = 100;
        _interfaceCombo.Top = 25;
        _interfaceCombo.Width = Math.Max(250, _refreshInterfacesButton.Left - _interfaceCombo.Left - gap);

        _interfaceView.Left = 15;
        _interfaceView.Width = Math.Max(400, _interfaceGroup.ClientSize.Width - 30);
    }

    private void BuildScanGroup()
    {
        _scanGroup.Text = "Scan Range";
        _scanGroup.Location = new Point(20, 278);
        _scanGroup.Size = new Size(1250, 118);
        _scanGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_scanGroup);

        _scanGroup.Controls.Add(new Label
        {
            Text = "Network/CIDR:",
            Location = new Point(15, 29),
            Size = new Size(95, 24)
        });

        _subnetBox.Text = "192.168.1.0/24";
        _subnetBox.Location = new Point(115, 26);
        _subnetBox.Size = new Size(265, 24);
        _scanGroup.Controls.Add(_subnetBox);

        _fullSubnetCheckBox.Text = "Full subnet";
        _fullSubnetCheckBox.Checked = true;
        _fullSubnetCheckBox.Location = new Point(400, 26);
        _fullSubnetCheckBox.Size = new Size(110, 24);
        _scanGroup.Controls.Add(_fullSubnetCheckBox);

        _scanGroup.Controls.Add(new Label
        {
            Text = "Timeout ms:",
            Location = new Point(525, 29),
            Size = new Size(80, 24)
        });

        _timeoutBox.Text = "500";
        _timeoutBox.Location = new Point(610, 26);
        _timeoutBox.Size = new Size(70, 24);
        _scanGroup.Controls.Add(_timeoutBox);

        _scanButton.Text = "Scan";
        _scanButton.Location = new Point(700, 24);
        _scanButton.Size = new Size(95, 30);
        _scanGroup.Controls.Add(_scanButton);

        _stopButton.Text = "Stop";
        _stopButton.Enabled = false;
        _stopButton.Location = new Point(805, 24);
        _stopButton.Size = new Size(95, 30);
        _scanGroup.Controls.Add(_stopButton);

        _discoverSubnetButton.Text = "Discover Subnet";
        _discoverSubnetButton.Location = new Point(920, 24);
        _discoverSubnetButton.Size = new Size(150, 30);
        _scanGroup.Controls.Add(_discoverSubnetButton);

        _scanGroup.Controls.Add(new Label
        {
            Text = "Legacy /24 range:",
            Location = new Point(15, 73),
            Size = new Size(115, 24)
        });

        _scanGroup.Controls.Add(new Label
        {
            Text = "Start:",
            Location = new Point(135, 73),
            Size = new Size(45, 24)
        });

        _startBox.Text = "1";
        _startBox.Location = new Point(185, 70);
        _startBox.Size = new Size(60, 24);
        _scanGroup.Controls.Add(_startBox);

        _scanGroup.Controls.Add(new Label
        {
            Text = "End:",
            Location = new Point(265, 73),
            Size = new Size(40, 24)
        });

        _endBox.Text = "254";
        _endBox.Location = new Point(310, 70);
        _endBox.Size = new Size(60, 24);
        _scanGroup.Controls.Add(_endBox);

        var hint = new Label
        {
            Text = "Uncheck Full subnet to scan a simple last-octet range.",
            Location = new Point(400, 73),
            Size = new Size(420, 24),
            ForeColor = UiTheme.MutedText
        };
        _scanGroup.Controls.Add(hint);

        _startBox.Enabled = !_fullSubnetCheckBox.Checked;
        _endBox.Enabled = !_fullSubnetCheckBox.Checked;
    }

    private void BuildResultsGrid()
    {
        _resultsGroup.Text = "Scan Results";
        _resultsGroup.Location = new Point(20, 410);
        _resultsGroup.Size = new Size(810, 340);
        _resultsGroup.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(_resultsGroup);

        _resultsView.Location = new Point(14, 26);
        _resultsView.Size = new Size(782, 298);
        _resultsView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _resultsView.View = View.Details;
        _resultsView.FullRowSelect = true;
        _resultsView.GridLines = false;
        _resultsView.HideSelection = false;
        _resultsView.Columns.Add("IP Address", 120);
        _resultsView.Columns.Add("Hostname", 205);
        _resultsView.Columns.Add("MAC Address", 145);
        _resultsView.Columns.Add("Manufacturer", 200);
        _resultsView.Columns.Add("Status", 95);
        _resultsGroup.Controls.Add(_resultsView);
    }

    private void BuildNetworkSettingsPanel()
    {
        _nicSettingsGroup.Text = "Selected NIC Settings";
        _nicSettingsGroup.Location = new Point(850, 410);
        _nicSettingsGroup.Size = new Size(420, 340);
        _nicSettingsGroup.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        Controls.Add(_nicSettingsGroup);

        AddSettingsLabel("NIC:", 30);
        _selectedNicBox.Location = new Point(130, 27);
        _selectedNicBox.Size = new Size(265, 24);
        _selectedNicBox.ReadOnly = true;
        _nicSettingsGroup.Controls.Add(_selectedNicBox);

        AddSettingsLabel("IP Address:", 68);
        _ipAddressBox.Location = new Point(130, 65);
        _ipAddressBox.Size = new Size(265, 24);
        _nicSettingsGroup.Controls.Add(_ipAddressBox);

        AddSettingsLabel("Subnet Mask:", 106);
        _subnetMaskBox.Location = new Point(130, 103);
        _subnetMaskBox.Size = new Size(265, 24);
        _nicSettingsGroup.Controls.Add(_subnetMaskBox);

        AddSettingsLabel("Gateway:", 144);
        _gatewayBox.Location = new Point(130, 141);
        _gatewayBox.Size = new Size(265, 24);
        _nicSettingsGroup.Controls.Add(_gatewayBox);

        AddSettingsLabel("Primary DNS:", 182);
        _primaryDnsBox.Location = new Point(130, 179);
        _primaryDnsBox.Size = new Size(265, 24);
        _nicSettingsGroup.Controls.Add(_primaryDnsBox);

        AddSettingsLabel("Secondary DNS:", 220);
        _secondaryDnsBox.Location = new Point(130, 217);
        _secondaryDnsBox.Size = new Size(265, 24);
        _nicSettingsGroup.Controls.Add(_secondaryDnsBox);

        _applyStaticButton.Text = "Apply Static";
        _applyStaticButton.Location = new Point(130, 270);
        _applyStaticButton.Size = new Size(125, 32);
        _nicSettingsGroup.Controls.Add(_applyStaticButton);

        _setDhcpButton.Text = "Set DHCP";
        _setDhcpButton.Location = new Point(270, 270);
        _setDhcpButton.Size = new Size(125, 32);
        _nicSettingsGroup.Controls.Add(_setDhcpButton);

        CenterNicSettingsButtons();
    }

    private void AddSettingsLabel(string text, int y)
    {
        _nicSettingsGroup.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(18, y),
            Size = new Size(105, 24)
        });
    }

    private void CenterNicSettingsButtons()
    {
        const int buttonGap = 18;

        var totalWidth = _applyStaticButton.Width + buttonGap + _setDhcpButton.Width;
        var left = Math.Max(18, (_nicSettingsGroup.ClientSize.Width - totalWidth) / 2);

        _applyStaticButton.Left = left;
        _setDhcpButton.Left = left + _applyStaticButton.Width + buttonGap;
    }

    private void BuildBottomControls()
    {
        _progressBar.Location = new Point(20, 770);
        _progressBar.Size = new Size(1250, 22);
        _progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_progressBar);

        _statusLabel.Text = "Ready.";
        _statusLabel.Location = new Point(20, 810);
        _statusLabel.Size = new Size(945, 28);
        _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(_statusLabel);

        _exportButton.Text = "Export Excel";
        _exportButton.Enabled = false;
        _exportButton.Location = new Point(1000, 803);
        _exportButton.Size = new Size(125, 32);
        _exportButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        Controls.Add(_exportButton);

        _clearButton.Text = "Clear";
        _clearButton.Location = new Point(1145, 803);
        _clearButton.Size = new Size(125, 32);
        _clearButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        Controls.Add(_clearButton);
    }

    private void WireEvents()
    {
        _refreshInterfacesButton.Click += (_, _) => RefreshInterfaces();
        _fullSubnetCheckBox.CheckedChanged += (_, _) => UpdateFullSubnetUi();
        _interfaceCombo.SelectedIndexChanged += (_, _) => SelectInterfaceFromCombo();
        _interfaceView.SelectedIndexChanged += (_, _) => SelectInterfaceFromListView();

        _scanButton.Click += async (_, _) => await StartScanAsync();
        _stopButton.Click += (_, _) => StopScan();
        _discoverSubnetButton.Click += (_, _) => ShowPassiveDiscovery();
        _clearButton.Click += (_, _) => ClearResults();
        _exportButton.Click += (_, _) => ExportExcel();
        _settingsButton.Click += (_, _) => ShowSettings();
        _resultsView.DoubleClick += (_, _) => OpenSelectedWebAddress();

        _applyStaticButton.Click += async (_, _) => await ApplyStaticSettingsAsync();
        _setDhcpButton.Click += async (_, _) => await SetDhcpAsync();

        FormClosed += (_, _) =>
        {
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _logoBox.Image?.Dispose();
        };
    }

    private void RefreshInterfaces()
    {
        _interfaces.Clear();
        _interfaceCombo.Items.Clear();
        _interfaceView.Items.Clear();

        var interfaces = NetworkInterfaceService.GetActiveIPv4Interfaces(AppSettingsService.Current.ShowLoopbackAdapters, AppSettingsService.Current.ShowDisconnectedAdapters);

        foreach (var iface in interfaces)
        {
            _interfaces.Add(iface);
            _interfaceCombo.Items.Add(iface);

            var dns = string.Join(", ", new[] { iface.PrimaryDns, iface.SecondaryDns }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var row = new ListViewItem(iface.InterfaceName);
            row.SubItems.Add(iface.IPv4Address);
            row.SubItems.Add(iface.SubnetMask);
            row.SubItems.Add(iface.AddressMethod);
            row.SubItems.Add(iface.OperationalStatus);
            row.SubItems.Add(iface.InterfaceType);
            row.SubItems.Add(iface.Gateway);
            row.SubItems.Add(string.IsNullOrWhiteSpace(dns) ? "None" : dns);
            row.SubItems.Add(iface.MacAddress);
            if (!string.Equals(iface.OperationalStatus, "Up", StringComparison.OrdinalIgnoreCase))
            {
                row.ForeColor = UiTheme.MutedText;
            }
            else if (iface.IsApipa)
            {
                row.ForeColor = UiTheme.Warning;
            }

            _interfaceView.Items.Add(row);
        }

        if (_interfaces.Count > 0)
        {
            _interfaceCombo.SelectedIndex = 0;
            SetStatus($"Found {_interfaces.Count} network adapter(s). APIPA/no-DHCP connected adapters are shown. Loopback: {(AppSettingsService.Current.ShowLoopbackAdapters ? "shown" : "hidden")}. Disconnected: {(AppSettingsService.Current.ShowDisconnectedAdapters ? "shown" : "hidden")}.");
        }
        else
        {
            ClearNicSettings();
            SetStatus("No network adapters found.");
        }
    }

    private NetworkInterfaceInfo? GetSelectedInterface()
    {
        var index = _interfaceCombo.SelectedIndex;
        return index >= 0 && index < _interfaces.Count ? _interfaces[index] : null;
    }

    private void SelectInterfaceFromCombo()
    {
        var selected = GetSelectedInterface();
        if (selected is null)
        {
            ClearNicSettings();
            return;
        }

        if (selected.HasIPv4)
        {
            var cidr = ScanTargetBuilder.GetCidrFromAddressAndMask(selected.IPv4Address, selected.SubnetMask);
            if (!string.IsNullOrWhiteSpace(cidr))
            {
                _subnetBox.Text = cidr;
            }
            else if (!string.IsNullOrWhiteSpace(selected.SubnetPrefix))
            {
                _subnetBox.Text = selected.SubnetPrefix;
            }
        }

        if (_interfaceView.Items.Count > _interfaceCombo.SelectedIndex)
        {
            foreach (ListViewItem item in _interfaceView.Items)
            {
                item.Selected = false;
            }

            _interfaceView.Items[_interfaceCombo.SelectedIndex].Selected = true;
            _interfaceView.Items[_interfaceCombo.SelectedIndex].EnsureVisible();
        }

        FillNicSettings(selected);

        var ipText = selected.HasIPv4 ? selected.IPv4Address : "No IPv4";
        var extra = selected.IsApipa ? " DHCP failed, APIPA address assigned." : "";
        SetStatus($"Selected {selected.InterfaceName}: {ipText}, {selected.AddressMethod}, link {selected.OperationalStatus}.{extra}");
    }

    private void SelectInterfaceFromListView()
    {
        if (_interfaceView.SelectedIndices.Count == 0)
        {
            return;
        }

        var index = _interfaceView.SelectedIndices[0];
        if (index >= 0 && index < _interfaceCombo.Items.Count)
        {
            _interfaceCombo.SelectedIndex = index;
        }
    }

    private void FillNicSettings(NetworkInterfaceInfo selected)
    {
        _selectedNicBox.Text = selected.InterfaceName;
        _ipAddressBox.Text = selected.HasIPv4 ? selected.IPv4Address : "";
        _subnetMaskBox.Text = selected.SubnetMask == "Unknown" ? "" : selected.SubnetMask;
        _gatewayBox.Text = selected.Gateway == "None" ? "" : selected.Gateway.Split(',')[0].Trim();
        _primaryDnsBox.Text = selected.PrimaryDns;
        _secondaryDnsBox.Text = selected.SecondaryDns;
    }

    private void ClearNicSettings()
    {
        _selectedNicBox.Clear();
        _ipAddressBox.Clear();
        _subnetMaskBox.Clear();
        _gatewayBox.Clear();
        _primaryDnsBox.Clear();
        _secondaryDnsBox.Clear();
    }

    private void ShowSettings()
    {
        UiTheme.SetMode(AppSettingsService.Current.ThemeMode);
        using var dialog = new SettingsForm(AppSettingsService.Current);
        var result = dialog.ShowDialog(this);

        if (result != DialogResult.OK)
        {
            return;
        }

        AppSettingsService.Current.ThemeMode = dialog.SelectedThemeMode;
        AppSettingsService.Current.ShowLoopbackAdapters = dialog.ShowLoopbackAdapters;
        AppSettingsService.Current.ShowDisconnectedAdapters = dialog.ShowDisconnectedAdapters;
        AppSettingsService.Save();

        UiTheme.SetMode(AppSettingsService.Current.ThemeMode);
        ApplyCurrentTheme();

        RefreshInterfaces();
        SetStatus($"Settings saved. Theme: {AppSettingsService.Current.ThemeMode}. Loopback: {(AppSettingsService.Current.ShowLoopbackAdapters ? "shown" : "hidden")}. Disconnected: {(AppSettingsService.Current.ShowDisconnectedAdapters ? "shown" : "hidden")}.");
    }

    private void ApplyCurrentTheme()
    {
        UiTheme.Apply(this);
        UiTheme.StyleSecondary(_refreshInterfacesButton);
        UiTheme.StyleSecondary(_stopButton);
        UiTheme.StyleSecondary(_clearButton);
        UiTheme.StyleSecondary(_setDhcpButton);
        ApplyHeadingFonts();
        StyleSettingsGear();
        CenterNicSettingsButtons();

        _titleLabel.ForeColor = UiTheme.Text;

        foreach (ListViewItem row in _resultsView.Items)
        {
            if (row.Tag is ScanResult result)
            {
                row.ForeColor = result.HasWebUi ? UiTheme.Success : UiTheme.Text;
            }
        }

        _settingsButton.Invalidate();
        _progressBar.Invalidate();
    }

    private void ShowPassiveDiscovery()
    {
        var selected = GetSelectedInterface();

        using var dialog = new PassiveDiscoveryForm(selected);
        var result = dialog.ShowDialog(this);

        if (result != DialogResult.OK)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(dialog.SelectedCidr))
        {
            _fullSubnetCheckBox.Checked = true;
            _subnetBox.Text = dialog.SelectedCidr;
            UpdateFullSubnetUi();
        }

        if (!string.IsNullOrWhiteSpace(dialog.SuggestedIp))
        {
            _ipAddressBox.Text = dialog.SuggestedIp;
        }

        if (!string.IsNullOrWhiteSpace(dialog.SuggestedMask))
        {
            _subnetMaskBox.Text = dialog.SuggestedMask;
        }

        SetStatus($"Passive discovery suggested {dialog.SelectedCidr}. Review the suggested static IP before applying NIC settings.");
    }

    private async Task ApplyStaticSettingsAsync()
    {
        var selected = GetSelectedInterface();
        if (selected is null)
        {
            MessageBox.Show("Select a network interface first.", "N8s IP Scanner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ValidateIPv4Box(_ipAddressBox.Text, "IP Address") ||
            !ValidateIPv4Box(_subnetMaskBox.Text, "Subnet Mask") ||
            !ValidateOptionalIPv4Box(_gatewayBox.Text, "Default Gateway") ||
            !ValidateOptionalIPv4Box(_primaryDnsBox.Text, "Primary DNS") ||
            !ValidateOptionalIPv4Box(_secondaryDnsBox.Text, "Secondary DNS"))
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Apply these static settings to '{selected.InterfaceName}'?\n\n" +
            $"IP: {_ipAddressBox.Text.Trim()}\n" +
            $"Mask: {_subnetMaskBox.Text.Trim()}\n" +
            $"Gateway: {BlankToNone(_gatewayBox.Text)}\n" +
            $"DNS 1: {BlankToNone(_primaryDnsBox.Text)}\n" +
            $"DNS 2: {BlankToNone(_secondaryDnsBox.Text)}\n\n" +
            "Windows will ask for administrator approval.",
            "Apply Static Network Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetNetworkSettingsUi(false);
            SetStatus("Applying static network settings...");

            await NetworkConfigurationService.ApplyStaticAsync(
                selected.InterfaceName,
                _ipAddressBox.Text.Trim(),
                _subnetMaskBox.Text.Trim(),
                _gatewayBox.Text.Trim(),
                _primaryDnsBox.Text.Trim(),
                _secondaryDnsBox.Text.Trim());

            await Task.Delay(1800);
            RefreshInterfaces();
            SetStatus("Static network settings applied.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Network Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Network settings were not applied.");
        }
        finally
        {
            SetNetworkSettingsUi(true);
        }
    }

    private async Task SetDhcpAsync()
    {
        var selected = GetSelectedInterface();
        if (selected is null)
        {
            MessageBox.Show("Select a network interface first.", "N8s IP Scanner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Set '{selected.InterfaceName}' to DHCP for IP address and DNS?\n\nWindows will ask for administrator approval.",
            "Set NIC to DHCP",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetNetworkSettingsUi(false);
            SetStatus("Setting selected NIC to DHCP...");

            await NetworkConfigurationService.SetDhcpAsync(selected.InterfaceName);

            await Task.Delay(1800);
            RefreshInterfaces();
            SetStatus("Selected NIC set to DHCP.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Network Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Network settings were not changed.");
        }
        finally
        {
            SetNetworkSettingsUi(true);
        }
    }

    private static bool ValidateIPv4Box(string value, string label)
    {
        if (IPAddress.TryParse(value.Trim(), out var parsed) &&
            parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            return true;
        }

        MessageBox.Show($"{label} must be a valid IPv4 address.", "Invalid Network Setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private static bool ValidateOptionalIPv4Box(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return ValidateIPv4Box(value, label);
    }

    private static string BlankToNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value.Trim();
    }

    private async Task StartScanAsync()
    {
        if (!TryReadScanSettings(out var targets, out var timeout, out var scanDescription))
        {
            return;
        }

        if (targets.Count > 4096)
        {
            var confirm = MessageBox.Show(
                $"This scan includes {targets.Count:N0} addresses.\n\n" +
                "Large subnet scans use a reliability-first profile with fewer simultaneous probes and retry logic, but they can still create noticeable network traffic.\n\n" +
                "Continue?",
                "Large Subnet Scan",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }
        }

        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();

        _results.Clear();
        _resultsView.Items.Clear();
        _exportButton.Enabled = false;
        _lastScanDescription = scanDescription;

        SetScanningUi(true);

        var total = targets.Count;
        var maxParallel = GetScanParallelism(total);
        _progressBar.Maximum = Math.Max(1, total);
        _progressBar.Value = 0;
        _progressBar.IsAnimating = true;

        var scanned = 0;
        var nextIndex = 0;
        var pending = new List<Task<(string IpAddress, ScanResult? Result)>>(maxParallel);

        try
        {
            SetStatus($"Reliability scan: {total:N0} address(es), up to {maxParallel} workers - {scanDescription}");

            while ((nextIndex < total || pending.Count > 0) && !_scanCancellation.IsCancellationRequested)
            {
                while (nextIndex < total &&
                       pending.Count < maxParallel &&
                       !_scanCancellation.IsCancellationRequested)
                {
                    var ipAddress = targets[nextIndex++];
                    pending.Add(ScanOneAsync(ipAddress, timeout, _scanCancellation.Token));
                }

                if (pending.Count == 0)
                {
                    break;
                }

                var completed = await Task.WhenAny(pending);
                pending.Remove(completed);

                var scanItem = await completed;
                scanned++;

                if (scanItem.Result is not null)
                {
                    _results.Add(scanItem.Result);
                    AddResultRow(scanItem.Result);
                }

                _progressBar.Value = Math.Min(scanned, _progressBar.Maximum);

                if (scanItem.Result is not null || scanned % 10 == 0 || scanned == total)
                {
                    SetStatus($"Reliability scan... {scanned:N0} of {total:N0} complete. Found {_results.Count:N0} device(s). Workers: {maxParallel}. {scanDescription}");
                }
            }
        }
        finally
        {
            _progressBar.IsAnimating = false;
            SetScanningUi(false);

            if (_scanCancellation.IsCancellationRequested)
            {
                SetStatus($"Stopped. Completed {scanned:N0} of {total:N0}. Found {_results.Count} device(s).");
            }
            else
            {
                SetStatus($"Scan complete. Scanned {total:N0} address(es). Found {_results.Count} device(s). Double-click HTTP/HTTPS rows to launch webpage.");
            }

            _exportButton.Enabled = _results.Count > 0;
        }
    }

    private async Task<(string IpAddress, ScanResult? Result)> ScanOneAsync(
        string ipAddress,
        int timeout,
        CancellationToken cancellationToken)
    {
        var result = await _scanner.ScanAsync(ipAddress, timeout, cancellationToken);
        return (ipAddress, result);
    }

    private static int GetScanParallelism(int totalTargets)
    {
        // Reliability-first profile. Fewer simultaneous probes reduce dropped replies
        // from slower embedded, AV, and management interfaces.
        if (totalTargets <= 32)
        {
            return Math.Max(1, Math.Min(totalTargets, 4));
        }

        if (totalTargets <= 256)
        {
            return 6;
        }

        if (totalTargets <= 1024)
        {
            return 8;
        }

        return 12;
    }

    private bool TryReadScanSettings(out List<string> targets, out int timeout, out string scanDescription)
    {
        targets = new List<string>();
        timeout = 0;
        scanDescription = "";

        var networkInput = _subnetBox.Text.Trim();

        if (!int.TryParse(_timeoutBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout) ||
            timeout < 50 ||
            timeout > 5000)
        {
            MessageBox.Show(
                "Timeout must be between 50 and 5000 milliseconds.",
                "Invalid Timeout",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_fullSubnetCheckBox.Checked)
        {
            if (!ScanTargetBuilder.TryBuildFullSubnetTargets(networkInput, out targets, out scanDescription, out var error))
            {
                MessageBox.Show(
                    error,
                    "Invalid Network/CIDR",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        if (!int.TryParse(_startBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !int.TryParse(_endBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            MessageBox.Show(
                "Start and End must be numbers between 0 and 255.",
                "Invalid Range",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (!ScanTargetBuilder.TryBuildLegacyRangeTargets(networkInput, start, end, out targets, out scanDescription, out var legacyError))
        {
            MessageBox.Show(
                legacyError,
                "Invalid Range",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void UpdateFullSubnetUi()
    {
        _startBox.Enabled = !_fullSubnetCheckBox.Checked;
        _endBox.Enabled = !_fullSubnetCheckBox.Checked;

        if (_fullSubnetCheckBox.Checked && !_subnetBox.Text.Contains('/'))
        {
            if (ScanTargetBuilder.TryNormalizeToCidr(_subnetBox.Text.Trim(), out var normalized))
            {
                _subnetBox.Text = normalized;
            }
        }
    }

    private void AddResultRow(ScanResult result)
    {
        var row = new ListViewItem(result.IPAddress)
        {
            Tag = result,
            ForeColor = result.HasWebUi ? UiTheme.Success : UiTheme.Text
        };

        row.SubItems.Add(result.Hostname);
        row.SubItems.Add(result.MacAddress);
        row.SubItems.Add(string.IsNullOrWhiteSpace(result.Manufacturer) ? "Unknown" : result.Manufacturer);
        row.SubItems.Add(result.Status);
        _resultsView.Items.Add(row);
    }

    private void OpenSelectedWebAddress()
    {
        if (_resultsView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_resultsView.SelectedItems[0].Tag is not ScanResult result)
        {
            return;
        }

        if (!result.HasWebUi)
        {
            SetStatus($"No HTTP or HTTPS web service detected on {result.IPAddress}.");
            return;
        }

        try
        {
            var url = result.PreferredUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            SetStatus($"Opened {url}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open webpage.\n{ex.Message}",
                "Open Webpage Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void StopScan()
    {
        _scanCancellation?.Cancel();
        SetStatus("Stopping after current address...");
    }

    private void ClearResults()
    {
        _results.Clear();
        _resultsView.Items.Clear();
        _exportButton.Enabled = false;
        _progressBar.Value = 0;
        SetStatus("Cleared scan results.");
    }

    private async Task UpdateOuiListAsync()
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
            SetStatus("Updating OUI manufacturer list from IEEE...");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var count = await OuiLookupService.UpdateFromIeeeAsync(timeout.Token);

            // Refresh manufacturer values already shown in the current results.
            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                var updated = new ScanResult
                {
                    IPAddress = result.IPAddress,
                    Hostname = result.Hostname,
                    MacAddress = result.MacAddress,
                    Manufacturer = OuiLookupService.Lookup(result.MacAddress),
                    Status = result.Status,
                    Port80Open = result.Port80Open,
                    Port443Open = result.Port443Open
                };

                _results[i] = updated;
            }

            _resultsView.Items.Clear();
            foreach (var result in _results)
            {
                AddResultRow(result);
            }

            SetStatus($"OUI list updated. Loaded {count:N0} manufacturer prefixes.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("OUI update canceled or timed out.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update the OUI list.\n\n{ex.Message}",
                "OUI Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SetStatus("OUI update failed. The built-in starter list is still available.");
        }
        finally
        {
            _updateOuiButton.Enabled = true;
        }
    }

    private void ExportExcel()
    {
        if (_results.Count == 0)
        {
            MessageBox.Show(
                "No results to export.",
                "N8s IP Scanner",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var saveDialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            FileName = "N8s-IPScan-Results.xlsx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ExcelExporter.Save(saveDialog.FileName, _results, _lastScanDescription);

            MessageBox.Show(
                $"Saved Excel scan results to:\n{saveDialog.FileName}",
                "Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not export Excel workbook.\n{ex.Message}",
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetScanningUi(bool scanning)
    {
        _scanButton.Enabled = !scanning;
        _stopButton.Enabled = scanning;
        _discoverSubnetButton.Enabled = !scanning;
        _clearButton.Enabled = !scanning;
        _refreshInterfacesButton.Enabled = !scanning;
        _settingsButton.Enabled = !scanning;
        _subnetBox.Enabled = !scanning;
        _fullSubnetCheckBox.Enabled = !scanning;
        _startBox.Enabled = !scanning && !_fullSubnetCheckBox.Checked;
        _endBox.Enabled = !scanning && !_fullSubnetCheckBox.Checked;
        _timeoutBox.Enabled = !scanning;
        _interfaceCombo.Enabled = !scanning;
        _interfaceView.Enabled = !scanning;
        SetNetworkSettingsUi(!scanning);
    }

    private void SetNetworkSettingsUi(bool enabled)
    {
        _applyStaticButton.Enabled = enabled;
        _setDhcpButton.Enabled = enabled;
        _ipAddressBox.Enabled = enabled;
        _subnetMaskBox.Enabled = enabled;
        _gatewayBox.Enabled = enabled;
        _primaryDnsBox.Enabled = enabled;
        _secondaryDnsBox.Enabled = enabled;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }
}
