using SQLite;

namespace MatchfishingApp.Data
{
    public sealed class MatchDb
    {
        private readonly SQLiteAsyncConnection _conn;

        public MatchDb(string dbPath)
        {
            _conn = new SQLiteAsyncConnection(dbPath);
        }

        public async Task InitializeAsync()
        {
            await _conn.CreateTableAsync<MatchRecord>();
            await _conn.CreateTableAsync<KeepnetRecord>();
            await _conn.CreateTableAsync<WeighEventRecord>();

            // Helpful index for event queries
            await _conn.ExecuteAsync(
                "CREATE INDEX IF NOT EXISTS IX_WeighEvent_MatchId ON WeighEvent (MatchId)");
        }

        // Start a match: deactivate others, insert match + keepnets, return new match id
        public async Task<int> StartMatchAsync(MatchRecord match, IEnumerable<KeepnetRecord> keepnets)
        {
            await _conn.ExecuteAsync("UPDATE Match SET IsActive = 0 WHERE IsActive = 1");

            match.IsActive = true;
            await _conn.InsertAsync(match);

            foreach (var kn in keepnets)
            {
                kn.MatchId = match.Id;
                await _conn.InsertAsync(kn);
            }

            return match.Id;
        }

        // Persist running totals (match + keepnets). Call after weight changes.
        public async Task SaveTotalsAsync(int matchId, double matchTotalLb, IEnumerable<KeepnetRecord> keepnets)
        {
            await _conn.ExecuteAsync("UPDATE Match SET TotalLb = ? WHERE Id = ?", matchTotalLb, matchId);

            foreach (var kn in keepnets)
            {
                if (kn.Id == 0)
                {
                    kn.MatchId = matchId;
                    await _conn.InsertAsync(kn);
                }
                else
                {
                    await _conn.UpdateAsync(kn);
                }
            }
        }

        // Append a weigh event (for analysis later)
        public Task LogWeighEventAsync(WeighEventRecord ev) => _conn.InsertAsync(ev);

        // Load the active match (if any) with its keepnets
        public async Task<(MatchRecord? match, List<KeepnetRecord> keepnets)> LoadActiveAsync()
        {
            var m = await _conn.Table<MatchRecord>().Where(x => x.IsActive).FirstOrDefaultAsync();
            if (m == null) return (null, new List<KeepnetRecord>());

            var kns = await _conn.Table<KeepnetRecord>().Where(k => k.MatchId == m.Id).ToListAsync();
            return (m, kns);
        }

        public Task<List<WeighEventRecord>> LoadEventsAsync(int matchId) =>
            _conn.Table<WeighEventRecord>().Where(e => e.MatchId == matchId).ToListAsync();

        // Mark a match as ended
        public Task EndMatchAsync(int matchId, long endUtcTicks) =>
            _conn.ExecuteAsync("UPDATE Match SET IsActive = 0, EndUtcTicks = ? WHERE Id = ?", endUtcTicks, matchId);

        // (Optional) Force-clear any active state (e.g., “Discard” flow)
        public Task DiscardActiveAsync() =>
            _conn.ExecuteAsync("UPDATE Match SET IsActive = 0 WHERE IsActive = 1");
    }
}
