using SQLite;

namespace MatchfishingApp.Data;

[Table("Match")]
public class MatchRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string? VenueName { get; set; }
    public string? LakeName { get; set; }
    public int PegNumber { get; set; }

    public long StartUtcTicks { get; set; }
    public long EndUtcTicks { get; set; }
    public int DurationMinutes { get; set; }

    public double TotalLb { get; set; }   // store pounds only
    public bool IsActive { get; set; }
}

[Table("Keepnet")]
public class KeepnetRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int MatchId { get; set; }

    public string NetName { get; set; } = "";
    public double WeightLimitLb { get; set; }
    public double TotalLb { get; set; }   // pounds
}

[Table("WeighEvent")]
public class WeighEventRecord
{
    [PrimaryKey, AutoIncrement] public long Id { get; set; }
    [Indexed] public int MatchId { get; set; }

    public long TimestampUtcTicks { get; set; }
    public double DeltaLb { get; set; }   // +lb for catches, -lb for corrections
    public int? KeepnetId { get; set; }
}
