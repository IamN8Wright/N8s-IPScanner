using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace N8sIPScanner;

public sealed class PassiveDiscoveryForm : Form
{
    private readonly NetworkInterfaceInfo? _selectedInterface;

    private readonly Label _introLabel = new();
    private readonly Label _durationLabel = new();
    private readonly TextBox _durationBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _pktmonButton = new();
    private readonly Button _useButton = new();
    private readonly Button _closeButton = new();
    private readonly ListView _observationsView = new();
    private readonly Label _statusLabel = new();

    private readonly Dictionary<string, ListViewItem> _itemsByKey = new();
    private readonly Dictionary<string, int> _countsByKey = new();

    private CancellationTokenSource? _discoveryCancellation;
    private bool _advancedCaptureRunning;

    public string SelectedCidr { get; private set; } = "";
    public string SuggestedIp { get; private set; } = "";
    public string SuggestedMask { get; private set; } = "";

    public PassiveDiscoveryForm(NetworkInterfaceInfo? selectedInterface)
    {
        _selectedInterface = selectedInterface;

        Text = "Passive Subnet Discovery";
        Icon = TryLoadIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1080, 680);

        UiTheme.SetMode(AppSettingsService.Current.ThemeMode);
        BuildInterface();
        WireEvents();

        Shown += async (_, _) => await StartDiscoveryAsync();
    }

    private static Icon? TryLoadIcon()
    {
        try
        {
            var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            return extracted;
        }
        catch
        {
            return null;
        }
    }

    private void BuildInterface()
    {
        SuspendLayout();

        _introLabel.Text =
            "Socket Passive listens for normal broadcast/multicast traffic. Advanced Passive Capture uses Windows Packet Monitor and may hear more, especially before your NIC has the right IP.";
        _introLabel.Location = new Point(16, 14);
        _introLabel.Size = new Size(1010, 44);
        _introLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_introLabel);

        var nicText = _selectedInterface is null
            ? "Selected NIC: All active IPv4 interfaces"
            : $"Selected NIC: {_selectedInterface.InterfaceName}   Current IP: {_selectedInterface.IPv4Address}";

        var nicLabel = new Label
        {
            Text = nicText,
            Location = new Point(16, 60),
            Size = new Size(1010, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(nicLabel);

        _durationLabel.Text = "Listen seconds:";
        _durationLabel.Location = new Point(16, 94);
        _durationLabel.Size = new Size(95, 24);
        Controls.Add(_durationLabel);

        _durationBox.Text = "60";
        _durationBox.Location = new Point(115, 91);
        _durationBox.Size = new Size(55, 24);
        Controls.Add(_durationBox);

        _startButton.Text = "Socket Passive";
        _startButton.Location = new Point(190, 89);
        _startButton.Size = new Size(115, 28);
        Controls.Add(_startButton);

        _stopButton.Text = "Stop";
        _stopButton.Location = new Point(315, 89);
        _stopButton.Size = new Size(75, 28);
        _stopButton.Enabled = false;
        Controls.Add(_stopButton);

        _pktmonButton.Text = "Advanced Capture";
        _pktmonButton.Location = new Point(405, 89);
        _pktmonButton.Size = new Size(145, 28);
        Controls.Add(_pktmonButton);

        _useButton.Text = "Use Selected Suggestion";
        _useButton.Location = new Point(565, 89);
        _useButton.Size = new Size(175, 28);
        _useButton.Enabled = false;
        Controls.Add(_useButton);

        _closeButton.Text = "Close";
        _closeButton.Location = new Point(935, 89);
        _closeButton.Size = new Size(90, 28);
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(_closeButton);

        _observationsView.Location = new Point(16, 130);
        _observationsView.Size = new Size(1010, 455);
        _observationsView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _observationsView.View = View.Details;
        _observationsView.FullRowSelect = true;
        _observationsView.GridLines = false;
        _observationsView.HideSelection = false;
        _observationsView.Columns.Add("Time", 80);
        _observationsView.Columns.Add("Source IP", 115);
        _observationsView.Columns.Add("Protocol", 90);
        _observationsView.Columns.Add("Suggested CIDR", 125);
        _observationsView.Columns.Add("Suggested NIC IP", 125);
        _observationsView.Columns.Add("Seen", 55);
        _observationsView.Columns.Add("Details", 420);
        Controls.Add(_observationsView);

        _statusLabel.Text = "Ready.";
        _statusLabel.Location = new Point(16, 600);
        _statusLabel.Size = new Size(1010, 24);
        _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_statusLabel);

        ResumeLayout(false);
        UiTheme.Apply(this);
        UiTheme.StyleSecondary(_stopButton);
        UiTheme.StyleSecondary(_closeButton);
        PerformLayout();
    }

    private void WireEvents()
    {
        _startButton.Click += async (_, _) => await StartDiscoveryAsync();
        _stopButton.Click += (_, _) => StopDiscovery();
        _pktmonButton.Click += async (_, _) => await RunAdvancedCaptureAsync();
        _closeButton.Click += (_, _) => Close();
        _useButton.Click += (_, _) => UseSelectedSuggestion();
        _observationsView.SelectedIndexChanged += (_, _) => _useButton.Enabled = _observationsView.SelectedItems.Count > 0;
        _observationsView.DoubleClick += (_, _) => UseSelectedSuggestion();

        FormClosing += (_, _) =>
        {
            _discoveryCancellation?.Cancel();
            _discoveryCancellation?.Dispose();
            _discoveryCancellation = null;
        };
    }

    private async Task StartDiscoveryAsync()
    {
        if (_discoveryCancellation is not null || _advancedCaptureRunning)
        {
            return;
        }

        if (!TryGetSeconds(out var seconds))
        {
            return;
        }

        ClearObservations();
        _discoveryCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        SetDiscoveryUi(true);
        SetStatus("Socket passive discovery is listening. No probes are being sent.");

        try
        {
            await PassiveSubnetDiscoveryService.ListenAsync(
                _selectedInterface,
                AddObservation,
                _discoveryCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal stop path.
        }
        finally
        {
            _discoveryCancellation?.Dispose();
            _discoveryCancellation = null;
            SetDiscoveryUi(false);

            if (_observationsView.Items.Count == 0)
            {
                SetStatus("No socket-level traffic heard. Try Advanced Capture, which uses Windows Packet Monitor.");
            }
            else
            {
                SetStatus($"Found {_observationsView.Items.Count} candidate observation(s). Select one and click Use Selected Suggestion.");
            }
        }
    }

    private async Task RunAdvancedCaptureAsync()
    {
        if (_discoveryCancellation is not null || _advancedCaptureRunning)
        {
            return;
        }

        if (!TryGetSeconds(out var seconds))
        {
            return;
        }

        var confirm = MessageBox.Show(
            "Advanced Passive Capture uses Windows Packet Monitor. It does not require Npcap, but Windows may ask for administrator approval.\n\nContinue?",
            "Advanced Passive Capture",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        ClearObservations();
        _advancedCaptureRunning = true;
        SetDiscoveryUi(true);
        _stopButton.Enabled = false;
        SetStatus($"Running Windows Packet Monitor for {seconds} seconds. Approve UAC if prompted.");

        try
        {
            var observations = await PktMonPassiveCaptureService.CaptureAsync(seconds);

            foreach (var observation in observations)
            {
                AddObservation(observation);
            }

            if (_observationsView.Items.Count == 0)
            {
                SetStatus("Advanced capture completed, but no usable IPv4 subnet clues were found. The switch port may be quiet or isolated.");
            }
            else
            {
                SetStatus($"Advanced capture found {_observationsView.Items.Count} subnet clue(s). Select one and click Use Selected Suggestion.");
            }
        }
        catch (OperationCanceledException ex)
        {
            MessageBox.Show(ex.Message, "Advanced Passive Capture", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus("Advanced capture canceled.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Advanced Passive Capture Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SetStatus("Advanced capture failed.");
        }
        finally
        {
            _advancedCaptureRunning = false;
            SetDiscoveryUi(false);
        }
    }

    private bool TryGetSeconds(out int seconds)
    {
        if (!int.TryParse(_durationBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) ||
            seconds < 5 ||
            seconds > 600)
        {
            MessageBox.Show(
                "Listen seconds must be between 5 and 600.",
                "Passive Subnet Discovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void StopDiscovery()
    {
        _discoveryCancellation?.Cancel();
        SetStatus("Stopping passive discovery...");
    }

    private void ClearObservations()
    {
        _itemsByKey.Clear();
        _countsByKey.Clear();
        _observationsView.Items.Clear();
        _useButton.Enabled = false;
    }

    private void AddObservation(PassiveDiscoveryObservation observation)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => AddObservation(observation)));
            }
            catch
            {
                // Form may be closing.
            }

            return;
        }

        var key = $"{observation.SourceIp}|{observation.Protocol}|{observation.SuggestedCidr}";

        if (_itemsByKey.TryGetValue(key, out var existing))
        {
            _countsByKey[key]++;
            existing.SubItems[0].Text = observation.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            existing.SubItems[5].Text = _countsByKey[key].ToString(CultureInfo.InvariantCulture);
            existing.SubItems[6].Text = observation.Details;
            return;
        }

        _countsByKey[key] = 1;

        var item = new ListViewItem(observation.Timestamp.ToLocalTime().ToString("HH:mm:ss"))
        {
            Tag = observation,
            ForeColor = UiTheme.Success
        };

        item.SubItems.Add(observation.SourceIp);
        item.SubItems.Add(observation.Protocol);
        item.SubItems.Add(observation.SuggestedCidr);
        item.SubItems.Add(observation.SuggestedIp);
        item.SubItems.Add("1");
        item.SubItems.Add(observation.Details);

        _itemsByKey[key] = item;
        _observationsView.Items.Add(item);
        item.EnsureVisible();

        SetStatus($"Heard {observation.Protocol} from {observation.SourceIp}. Suggested {observation.SuggestedCidr}.");
    }

    private void UseSelectedSuggestion()
    {
        if (_observationsView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_observationsView.SelectedItems[0].Tag is not PassiveDiscoveryObservation observation)
        {
            return;
        }

        SelectedCidr = observation.SuggestedCidr;
        SuggestedIp = observation.SuggestedIp;
        SuggestedMask = observation.SuggestedMask;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetDiscoveryUi(bool running)
    {
        _durationBox.Enabled = !running;
        _startButton.Enabled = !running;
        _pktmonButton.Enabled = !running;
        _stopButton.Enabled = running && _discoveryCancellation is not null;
        _closeButton.Enabled = !running;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }
}
