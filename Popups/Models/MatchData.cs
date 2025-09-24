using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;


namespace MatchfishingApp.Models
{
    public class MatchData : ObservableObject
    {
        public int MatchID { get; set; }
        public string? VenueName { get; set; }
        public string? LakeName { get; set; }
        public int PegNumber { get; set; }

        private int? _currentMatchId;
        public int? CurrentMatchId
        {
            get => _currentMatchId;
            set => SetProperty(ref _currentMatchId, value);
        }


        // (Optional) consider renaming this to MatchDateForDisplay, since we'll store actual timestamps below.
        public string? DateTime { get; set; }

        private int _totalMatchlb;
        public int TotalMatchlb
        {
            get => _totalMatchlb;
            set => SetProperty(ref _totalMatchlb, value);
        }

        private int _totalMatchoz;
        public int TotalMatchoz
        {
            get => _totalMatchoz;
            set => SetProperty(ref _totalMatchoz, value);
        }

        private TimeSpan _matchDuration;
        public TimeSpan MatchDuration
        {
            get => _matchDuration;
            set
            {
                if (SetProperty(ref _matchDuration, value))
                {
                    // If a match has already started, keep End in sync with new duration
                    UpdateEndFromStart();
                    // Keep your countdown aligned to the configured duration until Start is pressed
                    if (!MatchStartDateTime.HasValue)
                        TimeLeft = _matchDuration;
                }
            }
        }

        public int MatchDurationHours { get; set; }
        public int MatchDurationMinutes { get; set; }

        private TimeSpan _timeLeft;
        public TimeSpan TimeLeft
        {
            get => _timeLeft;
            set => SetProperty(ref _timeLeft, value);
        }

        // --- NEW: timestamps ---
        private DateTimeOffset? _matchStartDateTime;
        public DateTimeOffset? MatchStartDateTime
        {
            get => _matchStartDateTime;
            private set
            {
                if (SetProperty(ref _matchStartDateTime, value))
                {
                    UpdateEndFromStart();
                }
            }
        }

        private DateTimeOffset? _matchEndDateTime;
        public DateTimeOffset? MatchEndDateTime
        {
            get => _matchEndDateTime;
            private set => SetProperty(ref _matchEndDateTime, value);
        }

        public sealed class WeighEvent
        {
            public DateTimeOffset Timestamp { get; init; }
            public double DeltaLb { get; init; }   // switched to lb
            public int? KeepnetId { get; init; }
        }


        public List<WeighEvent> WeighEvents { get; } = new();
        //public void LogWeigh(int deltaOz, int? keepnetId = null)
        //    => WeighEvents.Add(new WeighEvent { Timestamp = DateTimeOffset.Now, DeltaOz = deltaOz, KeepnetId = keepnetId });

        public void LogWeighLb(double deltaLb, int? keepnetId = null)
            => WeighEvents.Add(new WeighEvent
            {
                Timestamp = DateTimeOffset.Now,
                DeltaLb = deltaLb,
                KeepnetId = keepnetId
            });

        public void RestoreFromPersistence(DateTimeOffset startLocal, TimeSpan duration, double? totalLb = null)
        {
            // apply duration first (keeps TimeLeft aligned pre-start)
            MatchDuration = duration;

            // set start via backing field (setter is private)
            _matchStartDateTime = startLocal;
            OnPropertyChanged(nameof(MatchStartDateTime));

            // compute end + time left
            UpdateEndFromStart();
            RecalculateTimeLeftFromSystemClock();

            // optional: if you loaded a saved total in lb
            if (totalLb.HasValue)
            {
                TotalMatchlb = (int)Math.Round(totalLb.Value);
                TotalMatchoz = 0;
            }
        }


        // --- /NEW ---

        public ObservableCollection<Net> Nets { get; set; } = new ObservableCollection<Net>
        {
            new Net { NetID = 1, NetName = "Keepnet 1", WeightLimit = 11 }
        };

        // Calculates MatchDuration from Hours/Minutes (kept from your original)
        public void CalculateMatchDuration()
        {
            MatchDuration = new TimeSpan(MatchDurationHours, MatchDurationMinutes, 0);
            TimeLeft = MatchDuration;
        }

        // --- NEW: control methods you call from UI ---
        /// Call this when the user taps Start on the MatchTracker pop-up.
        public void StartNow()
        {
            MatchStartDateTime = DateTimeOffset.Now;   // this also sets MatchEndDateTime
            RecalculateTimeLeftFromSystemClock();
        }

        /// Optional: clear timestamps (e.g., if user cancels/reset).
        public void ResetTimes()
        {
            MatchStartDateTime = null;
            MatchEndDateTime = null;
        }
        // --- /NEW ---

        // --- NEW: helper ---
        private void UpdateEndFromStart()
        {
            MatchEndDateTime = MatchStartDateTime.HasValue
                ? MatchStartDateTime.Value + MatchDuration
                : (DateTimeOffset?)null;
        }

        public void RecalculateTimeLeftFromSystemClock(Func<TimeSpan>? getScaledElapsed = null)
        {
            if (MatchEndDateTime.HasValue)
            {
                var now = DateTimeOffset.Now;
                // optional scaled “virtual now”:
                if (getScaledElapsed != null && MatchStartDateTime.HasValue)
                    now = MatchStartDateTime.Value + getScaledElapsed();

                var remaining = MatchEndDateTime.Value - now;
                TimeLeft = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            else
            {
                TimeLeft = MatchDuration;
            }
        }
        // --- /NEW ---
    }

    public class Net : ObservableObject
    {
        public int NetID { get; set; }
        public string NetName { get; set; }
        public int WeightLimit { get; set; }

        private int _netWeightlb;
        public int NetWeightlb
        {
            get => _netWeightlb;
            set => SetProperty(ref _netWeightlb, value);
        }

        private int _netWeightoz;
        public int NetWeightoz
        {
            get => _netWeightoz;
            set => SetProperty(ref _netWeightoz, value);
        }

        public bool IsAddKeepnetOption { get; set; }
    }
}
