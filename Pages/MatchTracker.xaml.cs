
using CommunityToolkit.Maui.Views;
using MatchfishingApp.Models;
using MatchfishingApp.Models.ViewModels;
using System.Diagnostics.Contracts;
using System.Globalization;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Microsoft.Maui.ApplicationModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions.EventArgs;
using System.Timers;
using System.Diagnostics;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MatchfishingApp.Data;
using MatchfishingApp.Popups; // add at top
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;


namespace MatchfishingApp.Pages;

public class BluetoothScanPermission : Permissions.BasePlatformPermission { }
public class BluetoothConnectPermission : Permissions.BasePlatformPermission { }


public partial class MatchTracker : ContentPage
{
    public TimeSpan _timeRemaining;
    private bool _isTimerRunning;
    public int _totalMatchlb;
    private readonly ILogger<MatchTracker> _logger;
    private MatchData _matchData;
    private MatchDataViewModel viewModel;
    private readonly Stopwatch _stopwatch = new();
    private readonly System.Timers.Timer _uiTimer = new(100) { AutoReset = true };
    private bool _isStopwatchRunningUi;

    private readonly MatchDb _db;
    private readonly bool _resumeConfirmed; // NEW



    /*
     * BLUETOOTH START
     */
    private IAdapter _bluetoothAdapter;
    private IDevice _connectedDevice;
    /*
     * BLUETOOTH END
     */

    public MatchTracker(
        MatchDataViewModel matchDataViewModel,
        ILogger<MatchTracker> logger,
        MatchDb db,
        bool resumeConfirmed = false) // NEW
    {
        InitializeComponent();

        _logger = logger;
        viewModel = matchDataViewModel;
        _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;
        _db = db;

        _resumeConfirmed = resumeConfirmed; // NEW

        BindingContext = viewModel;

        CalculateAndSetMatchDuration();
        UpdateTimeRemainingLabel();

        _uiTimer.Elapsed += OnUiTimerTick;

        spanTime.Text = "00:00";
        spanMs.Text = ".0";
        UpdateStopwatchUi(false);
        catchChart.AnimationsSpeed = TimeSpan.Zero;
        catchChart.EasingFunction = null;


        // Subscribe to the VolumeUpMessage.
        WeakReferenceMessenger.Default.Register<KeyPressedMessage>(this, (r, m) =>
        {
            if (m != null)
            {
                if (m.Value == "F2")
                {
                    // Ensure UI updates happen on the main thread.
                    Dispatcher.Dispatch(() =>
                    {
                        UpdateWeight(true, 0);
                        UpdateKeepnetWeight(true, 0);
                    });
                }

                if (m.Value == "F3")
                {
                    // Ensure UI updates happen on the main thread.
                    Dispatcher.Dispatch(() =>
                    {
                        UpdateWeight(true, 1);
                        UpdateKeepnetWeight(true, 1);
                    });
                }

                if (m.Value == "F4")
                {
                    // Ensure UI updates happen on the main thread.
                    Dispatcher.Dispatch(() =>
                    {
                        // Reset to zero and restart
                        _uiTimer.Stop();
                        _stopwatch.Restart();
                        Dispatcher.Dispatch(() =>
                        {
                            spanTime.Text = "00:00";
                            spanMs.Text = ".0";
                        });
                        _uiTimer.Start();
                    });
                }




            }
        });

    }

    private void UpdateStopwatchUi(bool isRunning)
    {
        _isStopwatchRunningUi = isRunning;
        StopwatchStartPanel.IsVisible = !isRunning;
        StopwatchRunningPanel.IsVisible = isRunning;
    }


    // New handler for the popup’s Yes button
    private async void btnStartMatchPopup_Clicked(object sender, EventArgs e)
    {
        // Hide the overlay
        overlayGrid.IsVisible = false;
        // Re-enable everything underneath
        mainContent.IsEnabled = true;

        // Start the match
        StartMatch();

        btnStartMatchPopup.IsEnabled = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // --- CASE A: We arrived here from Home with "Resume" already confirmed ---
        if (_resumeConfirmed)
        {
            var (active, keepnets) = await _db.LoadActiveAsync();
            if (active != null)
            {
                // Restore persisted match into the in-memory model
                var startLocal = new DateTimeOffset(new DateTime(active.StartUtcTicks, DateTimeKind.Utc)).ToLocalTime();
                var duration = TimeSpan.FromMinutes(active.DurationMinutes);

                var md = viewModel.MatchData;
                md.RestoreFromPersistence(startLocal, duration, active.TotalLb);

                md.Nets.Clear();
                foreach (var k in keepnets)
                {
                    md.Nets.Add(new Net
                    {
                        NetName = k.NetName,
                        WeightLimit = (int)Math.Round(k.WeightLimitLb),
                        NetWeightlb = (int)Math.Round(k.TotalLb),
                        NetWeightoz = 0
                    });
                }
                md.CurrentMatchId = active.Id;

                // Hide/disable any "start match" overlay for resume flow
                overlayGrid.IsVisible = false;
                mainContent.IsEnabled = true;
                btnStartMatchPopup.IsEnabled = false;

                // Refresh UI + (re)start countdown if needed
                md.RecalculateTimeLeftFromSystemClock();
                RebuildCatchChart();
                if (md.TimeLeft > TimeSpan.Zero && !_isTimerRunning)
                    StartTimer();
            }

            // Do your sizing regardless
            var disp = DeviceDisplay.MainDisplayInfo;
            double widthDp = disp.Width / disp.Density;
            lblTimeRemaining.FontSize = widthDp * 0.20;

            UpdateTimeRemainingLabel();
            return; // IMPORTANT: skip the default popup logic below
        }

        // --- CASE B: Default behavior (navigated here without pre-confirmed resume) ---
        // This preserves your existing popup flow when you open MatchTracker directly
        // (e.g., from a new match setup or deep link).
        var (active2, keepnets2) = await _db.LoadActiveAsync();
        if (active2 != null)
        {
            var popup = new ResumeActiveMatchPopup();
            var choice = await this.ShowPopupAsync(popup) as string; // "resume" or "discard"

            if (choice == "resume")
            {
                var startLocal = new DateTimeOffset(new DateTime(active2.StartUtcTicks, DateTimeKind.Utc)).ToLocalTime();
                var duration = TimeSpan.FromMinutes(active2.DurationMinutes);

                var md = viewModel.MatchData;
                md.RestoreFromPersistence(startLocal, duration, active2.TotalLb);

                md.Nets.Clear();
                foreach (var k in keepnets2)
                {
                    md.Nets.Add(new Net
                    {
                        NetName = k.NetName,
                        WeightLimit = (int)Math.Round(k.WeightLimitLb),
                        NetWeightlb = (int)Math.Round(k.TotalLb),
                        NetWeightoz = 0
                    });
                }
                md.CurrentMatchId = active2.Id;

                // Also suppress the "start match" overlay when resuming here
                overlayGrid.IsVisible = false;
                mainContent.IsEnabled = true;
                btnStartMatchPopup.IsEnabled = false;

                md.RecalculateTimeLeftFromSystemClock();
                RebuildCatchChart();
                if (md.TimeLeft > TimeSpan.Zero && !_isTimerRunning)
                    StartTimer();
            }
            else if (choice == "discard")
            {
                await _db.DiscardActiveAsync();

                // Clear in-memory state for a clean slate
                var md = viewModel.MatchData;
                md.ResetTimes();
                md.TotalMatchlb = 0; md.TotalMatchoz = 0;
                foreach (var n in md.Nets) { n.NetWeightlb = 0; n.NetWeightoz = 0; }
                md.CurrentMatchId = null;

                // For a new match (fresh start) you may want the overlay shown/enabled here.
                // If your XAML defaults to visible, no need to toggle it explicitly.
            }
        }

        // Sizing + initial UI refresh (as in your original code)
        var disp2 = DeviceDisplay.MainDisplayInfo;
        double widthDp2 = disp2.Width / disp2.Density;
        lblTimeRemaining.FontSize = widthDp2 * 0.20;

        // Your original behavior had this disabled by default until starting:
        mainContent.IsEnabled = false;

        viewModel.MatchData.RecalculateTimeLeftFromSystemClock();
        UpdateTimeRemainingLabel();
        RebuildCatchChart();
    }




    //warpspeed start
    private readonly Stopwatch _fastClock = new();
    private const double TIME_SCALE = 1.0; // 10x speed for testing
    //warpspeed end
    private async Task<bool> CheckAndRequestBluetoothPermissionsAsync()
    {
        // Only needed for Android 12 and above
        if (OperatingSystem.IsAndroidVersionAtLeast(12))
        {
            var scanStatus = await Permissions.CheckStatusAsync<BluetoothScanPermission>();
            if (scanStatus != PermissionStatus.Granted)
            {
                scanStatus = await Permissions.RequestAsync<BluetoothScanPermission>();
                if (scanStatus != PermissionStatus.Granted)
                    return false;
            }

            var connectStatus = await Permissions.CheckStatusAsync<BluetoothConnectPermission>();
            if (connectStatus != PermissionStatus.Granted)
            {
                connectStatus = await Permissions.RequestAsync<BluetoothConnectPermission>();
                if (connectStatus != PermissionStatus.Granted)
                    return false;
            }
        }
        return true;
    }




    private async void OnAddKeepnetClicked(object sender, EventArgs e)
    {
        // Get the first keepnet to pre-populate the popup
        var firstKeepnet = viewModel.MatchData.Nets.FirstOrDefault();

        if (firstKeepnet != null)
        {
            // Show the popup and pass the first keepnet's data
            var popup = new AddKeepnetPopup(firstKeepnet);
            var result = await this.ShowPopupAsync(popup);

            if (result is Net newKeepnet)
            {
                // If there are no keepnets, assign NetID = 1
                if (viewModel.MatchData.Nets.Count == 0)
                {
                    newKeepnet.NetID = 1;
                }
                else
                {
                    // Otherwise, find the next highest NetID
                    int nextNetID = viewModel.MatchData.Nets.Max(n => n.NetID) + 1;
                    newKeepnet.NetID = nextNetID;
                }

                // Add the new keepnet before the "Add Keepnet" option
                viewModel.MatchData.Nets.Insert(viewModel.MatchData.Nets.Count, newKeepnet);

                // Explicitly refresh the CarouselView by resetting its ItemsSource
                carouselView.ItemsSource = null;
                carouselView.ItemsSource = viewModel.MatchData.Nets;

                // Wait briefly to ensure the layout is ready
                await Task.Delay(400);

                // Scroll to the newly added keepnet (second last, before the "Add Keepnet" option)
                carouselView.ScrollTo(viewModel.MatchData.Nets.Count -1, position: ScrollToPosition.Center, animate: false);
            }
        }
    }

    private void CalculateAndSetMatchDuration()
    {
        // Convert the hours and minutes into a TimeSpan
        viewModel.MatchData.CalculateMatchDuration();
    }

    private void btnStartMatch_Clicked(object sender, EventArgs e)
    {
        StartMatch();
    }

    private void StartMatch()
    {
        if (!_isTimerRunning)
        {
            viewModel.MatchData.StartNow(); // sets start/end

            _ = StartMatchDbInsertAsync();  // <-- fire-and-forget insert

            viewModel.MatchData.RecalculateTimeLeftFromSystemClock();
            RebuildCatchChart();
            StartTimer();

        }
    }


    private void btnStopwatchStart_Clicked(object sender, EventArgs e)
    {
        // Fresh start from 00:00
        _uiTimer.Stop();
        _stopwatch.Reset();
        _stopwatch.Start();

        // Update UI immediately
        spanTime.Text = "00:00";
        spanMs.Text = ".0";

        // Start the refresh timer
        if (!_uiTimer.Enabled) _uiTimer.Start();

        // Switch to icon buttons
        UpdateStopwatchUi(true);
    }


    private void StartTimer()
    {
        _isTimerRunning = true;

        Application.Current.Dispatcher.StartTimer(TimeSpan.FromMilliseconds(200), () => // faster tick
        {
            if (!_isTimerRunning) return false;



            viewModel.MatchData.RecalculateTimeLeftFromSystemClock();


            // update UI
            Dispatcher.Dispatch(UpdateTimeRemainingLabel);

            if (viewModel.MatchData.TimeLeft <= TimeSpan.Zero)
            {
                _isTimerRunning = false;

                // NEW: mark DB record inactive (don’t block UI)
                if (viewModel.MatchData.CurrentMatchId is int mid)
                    _ = _db.EndMatchAsync(mid, DateTimeOffset.UtcNow.Ticks);

                return false;
            }

            return true;
        });
    }





    private void btnPlus1lb_Clicked(object sender, EventArgs e) => UpdateWeightLb(1.0);
    private void btnMinus1lb_Clicked(object sender, EventArgs e) => UpdateWeightLb(-1.0);

    // Keep these temporarily if you still show oz buttons.
    // When you remove oz from the UI, delete these two lines.
    private void btnPlus1oz_Clicked(object sender, EventArgs e) => UpdateWeightLb(1.0 / 16.0);
    private void btnMinus1oz_Clicked(object sender, EventArgs e) => UpdateWeightLb(-1.0 / 16.0);





    // NEW: push all weight changes through here, using pounds
    private void UpdateWeightLb(double deltaLb)
    {
        var md = viewModel.MatchData;

        // --- Match total (lb only) ---
        double newMatchLb = md.TotalMatchlb + deltaLb;
        if (newMatchLb < 0) newMatchLb = 0;
        md.TotalMatchlb = (int)Math.Round(newMatchLb);   // keep as int lb for now
        md.TotalMatchoz = 0;                             // zero out ounces moving forward

        // --- Active keepnet (lb only) ---
        var kn = md.Nets[activeKeepnetIndex];
        double newNetLb = kn.NetWeightlb + deltaLb;
        if (newNetLb < 0) newNetLb = 0;
        kn.NetWeightlb = (int)Math.Round(newNetLb);
        kn.NetWeightoz = 0;

        // In-memory event for the chart (now in lb):
        viewModel.MatchData.LogWeighLb(deltaLb);

        // Persist to SQLite (we already set up lb in Step 4)
        _ = PersistWeighAsync(deltaLb);

        // Refresh UI that depends on totals
        RebuildCatchChart();
        _logger.LogInformation("Weight updated by {deltaLb} lb. New total: {totalLb} lb",
            deltaLb, md.TotalMatchlb);
    }




    private void UpdateWeight(bool increase, int unit)
    {
        _logger.LogInformation("Weight Updated. Increase: {increase}, Amount: {unit}");

        if (increase)
        {
            if (unit == 1)//lb up
            {
                var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                viewModel.MatchData.TotalMatchlb += 1;  // Update the weight
            }
            else//oz up
            {
                var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                                                                     // Increment ounces and roll over to pounds when ounces hit 16
                if (viewModel.MatchData.TotalMatchoz >= 15)
                {
                    viewModel.MatchData.TotalMatchlb += 1;
                    viewModel.MatchData.TotalMatchoz = 0;
                }
                else
                {
                    viewModel.MatchData.TotalMatchoz += 1;
                }
            }
        }
        else
        {
            if (unit == 1)//lb Down
            {
                var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                viewModel.MatchData.TotalMatchlb -= 1;  // Update the weight
            }
            else//oz down
            {
                var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                if (viewModel.MatchData.TotalMatchoz == 0)
                {
                    viewModel.MatchData.TotalMatchlb -= 1;
                    viewModel.MatchData.TotalMatchoz = 15;
                }
                else
                {
                    viewModel.MatchData.TotalMatchoz -= 1;
                }
            }
        }

        // compute delta in ounces for the chart log
        int deltaOz = increase
            ? (unit == 1 ? 16 : 1)   // +1 lb or +1 oz
            : (unit == 1 ? -16 : -1);// -1 lb or -1 oz

        double deltaLb = increase
            ? (unit == 1 ? 1.0 : 1.0 / 16.0)    // +1 lb or +1 oz (for now)
            : (unit == 1 ? -1.0 : -1.0 / 16.0); // -1 lb or -1 oz

        viewModel.MatchData.LogWeighLb(deltaLb);

        _ = PersistWeighAsync(deltaLb);

        RebuildCatchChart(); // update the chart


        _logger.LogInformation("Weight updated. New TotalMatchlb: {totalMatchlb}", viewModel.MatchData.TotalMatchlb);
    }

    private const double BIN_MINUTES = 30.0; // 1-minute bins for testing (was 30)

    private void RebuildCatchChart()
    {
        var md = viewModel.MatchData;
        // guard rails
        if (!md.MatchStartDateTime.HasValue || md.MatchDuration <= TimeSpan.Zero)
        {
            catchChart.Series = Array.Empty<ISeries>();
            catchChart.XAxes = new[] { new LiveChartsCore.SkiaSharpView.Axis { Labels = Array.Empty<string>() } };
            return;
        }

        var start = md.MatchStartDateTime.Value;
        var bins = (int)Math.Ceiling(md.MatchDuration.TotalMinutes / BIN_MINUTES);
        if (bins <= 0) bins = 1;

        var valuesLb = new double[bins];

        foreach (var ev in md.WeighEvents)
        {
            if (ev.DeltaLb <= 0) continue; // only count positive catches (ignore corrections)

            var minsFromStart = (ev.Timestamp - start).TotalMinutes;
            if (minsFromStart < 0) continue;

            var ix = (int)Math.Floor(minsFromStart / BIN_MINUTES);
            if (ix >= bins) ix = bins - 1; // clamp just-overflow events

            valuesLb[ix] += ev.DeltaLb; // <-- now pounds directly
        }


        // labels for the X axis (unchanged)
        var labels = Enumerable.Range(1, bins)
            .Select(i => TimeSpan.FromMinutes(i * BIN_MINUTES))
            .Select(ts => $"{(int)ts.TotalHours}:{ts.Minutes:00}")
            .ToArray();


        // build the column series with value labels on each bar test
        var series = new ColumnSeries<double>
        {
            Name = "Catch / 30m",
            Values = valuesLb,

            // labels (keep your formatter change)
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            DataLabelsPosition = DataLabelsPosition.Middle,
            DataLabelsFormatter = p => $"{p.Model:0} lb",

            // turn off series animations
            AnimationsSpeed = TimeSpan.Zero,
            EasingFunction = null
        };

        catchChart.Series = new ISeries[] { series };

        // keep X labels as before
        catchChart.XAxes = new[]
        {
    new LiveChartsCore.SkiaSharpView.Axis
    {
        Labels = labels
    }
};

        // hide Y axis entirely
        catchChart.YAxes = new[]
        {
    new LiveChartsCore.SkiaSharpView.Axis
    {
        IsVisible = false
    }
};
    }


    private void UpdateKeepnetWeight(bool increase, int unit)
    {
        var activeKeepnet = viewModel.MatchData.Nets[activeKeepnetIndex];
        if (activeKeepnetIndex >= 0 && activeKeepnetIndex < viewModel.MatchData.Nets.Count)
            {         
            if (increase)
            {
                if (unit == 1)//lb up
                {
                    var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                    activeKeepnet.NetWeightlb += 1;
                }
                else//oz up
                {
                    var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                                                                         // Increment ounces and roll over to pounds when ounces hit 16
                    if (activeKeepnet.NetWeightoz >= 15)
                    {
                        activeKeepnet.NetWeightlb += 1;
                        activeKeepnet.NetWeightoz = 0;
                    }
                    else
                    {
                        activeKeepnet.NetWeightoz += 1;
                    }
                }
            }
            else
            {
                if (unit == 1)//lb Down
                {
                    var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                    activeKeepnet.NetWeightlb -= 1;  // Update the weight
                }
                else//oz down
                {
                    var viewModel = (MatchDataViewModel)BindingContext;  // Access the view model
                    if (activeKeepnet.NetWeightoz == 0)
                    {
                        activeKeepnet.NetWeightlb -= 1;
                        activeKeepnet.NetWeightoz = 15;
                    }
                    else
                    {
                        activeKeepnet.NetWeightoz -= 1;
                    }
                }
            }
        }
        ColourKeepnetLabel(activeKeepnet);
    }

    private int activeKeepnetIndex = 0; // This will store the active keepnet index
    private void OnCarouselPositionChanged(object sender, PositionChangedEventArgs e)
    {
        var carousel = sender as CarouselView;
        // Ensure that the carousel snaps to the exact item
        carousel.ScrollTo(e.CurrentPosition, position: ScrollToPosition.Center, animate: true);

        activeKeepnetIndex = e.CurrentPosition;

        int currentPosition = e.CurrentPosition;
        // Do something with the position, for example, log it or update a label
        Console.WriteLine($"Current Carousel Position: {currentPosition}");

        var activeKeepnet = viewModel.MatchData.Nets[currentPosition];
        ColourKeepnetLabel(activeKeepnet);
    }

    public void ColourKeepnetLabel(Net activeKeepnet)
    {
        var carouselFrame = this.FindByName<Frame>("carouselFrame");
        if (activeKeepnet.NetWeightlb >= activeKeepnet.WeightLimit)
        {
            carouselFrame.BackgroundColor = Colors.Red;
        }
        else if (activeKeepnet.NetWeightlb + 10 >= activeKeepnet.WeightLimit)
        {
            carouselFrame.BackgroundColor = Colors.Orange;
        }
        else
        {
            carouselFrame.BackgroundColor = Colors.LightGreen;
        }
    }


    private void UpdateTimeRemainingLabel()
    {
        // Get current time left and clamp at zero
        var t = viewModel.MatchData.TimeLeft;
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;

        // Build HH:MM:SS, with smaller grey seconds
        lblTimeRemaining.FormattedText = new FormattedString
        {
            Spans =
        {
            new Span { Text = $"{t.Hours:D1}:",   FontSize = 130, TextColor = Colors.White, FontFamily="DigitalFont"},
            new Span { Text = $"{t.Minutes:D2}.", FontSize = 130, TextColor = Colors.White, FontFamily="DigitalFont"},
            new Span { Text = $"{t.Seconds:D2}",  FontSize = 35, TextColor = Colors.LightGrey, FontFamily="Monospace" }
        }
        };
    }



    private void OnUiTimerTick(object? sender, ElapsedEventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        var minutes = elapsed.Minutes;
        var seconds = elapsed.Seconds;
        // integer division: 0–999 ms becomes 0–9
        var tenths = elapsed.Milliseconds / 100;
        Dispatcher.Dispatch(() =>
        {
            var t = viewModel.MatchData.TimeLeft;

            Dispatcher.Dispatch(() =>
            {
                var e = _stopwatch.Elapsed;
                spanTime.Text = $"{e.Minutes:00}:{e.Seconds:00}";
                spanMs.Text = $".{e.Milliseconds / 100}";
            });

        });


    }


    private void btnStopwatchReset_Clicked(object sender, EventArgs e)
    {
        // keep running, just go back to 00:00
        _uiTimer.Stop();
        _stopwatch.Reset();
        _stopwatch.Start();

        spanTime.Text = "00:00";
        spanMs.Text = ".0";

        if (!_uiTimer.Enabled) _uiTimer.Start();

        UpdateStopwatchUi(true); // stays in running mode
    }

    private void btnStopwatchStop_Clicked(object sender, EventArgs e)
    {
        _uiTimer.Stop();
        _stopwatch.Stop();
        _stopwatch.Reset();

        spanTime.Text = "00:00";
        spanMs.Text = ".0";

        UpdateStopwatchUi(false); // back to single Start button
    }

    //SQLite Logic Start
    private async Task StartMatchDbInsertAsync()
    {
        var md = viewModel.MatchData;

        var startUtc = md.MatchStartDateTime!.Value.UtcDateTime.Ticks;
        var endUtc = md.MatchEndDateTime!.Value.UtcDateTime.Ticks;

        var matchRec = new MatchRecord
        {
            VenueName = md.VenueName,
            LakeName = md.LakeName,
            PegNumber = md.PegNumber,
            StartUtcTicks = startUtc,
            EndUtcTicks = endUtc,
            DurationMinutes = (int)md.MatchDuration.TotalMinutes,
            TotalLb = md.TotalMatchlb, // store pounds only
            IsActive = true
        };

        var keepnetRecs = md.Nets.Select(n => new KeepnetRecord
        {
            NetName = n.NetName,
            WeightLimitLb = n.WeightLimit,
            TotalLb = n.NetWeightlb
        });

        var matchId = await _db.StartMatchAsync(matchRec, keepnetRecs);
        md.CurrentMatchId = matchId; // remember DB key in RAM
    }


    private async Task PersistWeighAsync(double deltaLb)
    {
        var md = viewModel.MatchData;
        if (md.CurrentMatchId is not int mid) return;

        // 1) append weigh event
        await _db.LogWeighEventAsync(new WeighEventRecord
        {
            MatchId = mid,
            TimestampUtcTicks = DateTimeOffset.UtcNow.Ticks,
            DeltaLb = deltaLb,
            KeepnetId = null // wire this if/when you track KeepnetRecord.Id
        });

        // 2) save running totals for match + keepnets
        var keepnets = md.Nets.Select(n => new KeepnetRecord
        {
            // if you later track KeepnetRecord.Id, set it so UpdateAsync can work
            MatchId = mid,
            NetName = n.NetName,
            WeightLimitLb = n.WeightLimit,
            TotalLb = n.NetWeightlb
        });

        await _db.SaveTotalsAsync(mid, md.TotalMatchlb, keepnets);
    }

    //SQLite Logic End



    /*
     * BLUETOOTH LOGIC START
     * 
     */




    private async void SubscribeToButtonPress()
    {
        if (_connectedDevice == null)
        {
            System.Diagnostics.Debug.WriteLine("No connected device.");
            _logger.LogWarning("SubscribeToButtonPress called, but _connectedDevice is null.");
            return;
        }

        // Use the manufacturer-provided GUIDs for your remote:
        var serviceGuid = Guid.Parse("YOUR-CUSTOM-SERVICE-GUID");
        var characteristicGuid = Guid.Parse("YOUR-CUSTOM-CHARACTERISTIC-GUID");

        var service = await _connectedDevice.GetServiceAsync(serviceGuid);
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine("Custom service not found.");
            _logger.LogWarning("Custom service not found for GUID: {serviceGuid}", serviceGuid);

            return;
        }

        var characteristic = await service.GetCharacteristicAsync(characteristicGuid);
        if (characteristic == null)
        {
            System.Diagnostics.Debug.WriteLine("Custom characteristic not found.");
            _logger.LogWarning("Custom characteristic not found for GUID: {characteristicGuid}", characteristicGuid);

            return;
        }

        characteristic.ValueUpdated += (s, e) =>
        {
            var data = e.Characteristic.Value;
            _logger.LogInformation("BLE notification received with data: {data}", BitConverter.ToString(data));

            // Interpret data as per your remote's spec.
            if (data != null && data.Length > 0 && data[0] == 1)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    _logger.LogInformation("Custom button press detected via BLE notification.");

                    UpdateWeight(true, 1);
                    UpdateKeepnetWeight(true, 1);
                });
            }
        };

        await characteristic.StartUpdatesAsync();
        System.Diagnostics.Debug.WriteLine("Subscribed to custom button notifications.");
        _logger.LogInformation("Subscribed to custom button notifications.");

    }





        // 1) Update the BLE-connection label when you connect:
        private async void OnDeviceDiscovered(object sender, DeviceEventArgs e)
        {
            if (e.Device.Name != null)
            {
                //_connectedDevice = e.Device;
                //lblDebugBleStatus.Text = $"BLE: Discovered {e.Device.Name}";
                //await _bluetoothAdapter.StopScanningForDevicesAsync();
                //await _bluetoothAdapter.ConnectToDeviceAsync(_connectedDevice);
                //lblDebugBleStatus.Text = $"BLE: Connected to {_connectedDevice.Name}";
                //SubscribeToButtonPress();
            }
        }
    }





    /*
    * BLUETOOTH LOGIC END
    *   
    */
