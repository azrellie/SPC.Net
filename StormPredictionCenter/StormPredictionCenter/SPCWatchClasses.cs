namespace Azrellie.Meteorology.SPC;

public record WatchHazard(int chance, string hazard)
{
	public int chance = chance;
	public string hazard = hazard;
}
public record WatchHazards
{
	public string message = string.Empty;
	public bool isPDS = false;
	public WatchHazard tornadoes = new(0, string.Empty);
	public WatchHazard ef2PlusTornadoes = new(0, string.Empty);
	public WatchHazard severeWind = new(0, string.Empty);
	public WatchHazard _65ktPlusWind = new(0, string.Empty);
	public WatchHazard severeHail = new(0, string.Empty);
	public WatchHazard _2InchPlusHail = new(0, string.Empty);
	public override string ToString()
	{
		string pds = string.Empty;
		if (isPDS)
			pds = "PDS | ";
		return pds + $"Tornadoes: {tornadoes.chance}% | EF2+ Tornadoes: {ef2PlusTornadoes.chance}% | Severe Wind: {severeWind.chance}% | 65 kt+ Wind: {_65ktPlusWind.chance}% | Severe Hail: {severeHail.chance}% | 2\"+ Hail: {_2InchPlusHail.chance}%";
	}
}
public record CountyInfo
{
	public string id = string.Empty;
	public string name = string.Empty;
	public string state = string.Empty;
	public List<string> forecastOffices = [];
	public string timeZone = string.Empty;
	public SPCPolygon geometry = new();
	public override string ToString() => $"{name} county, {state} - {timeZone} - {id}";
}
public record StormPredictionCenterWatch
{
	public DateTimeOffset sent = DateTimeOffset.UnixEpoch;
	public DateTimeOffset effective = DateTimeOffset.UnixEpoch;
	public DateTimeOffset onset = DateTimeOffset.UnixEpoch;
	public DateTimeOffset expires = DateTimeOffset.UnixEpoch;
	public DateTimeOffset ends = DateTimeOffset.UnixEpoch;
	public double[] watchCenter = new double[2];
	public int watchNumber = 0;
	public string description = string.Empty;
	public string sender = string.Empty;
	public string headline = string.Empty;
	public string watchType = string.Empty;
	public WarningEventType status = WarningEventType.NewIssue;
	public WatchHazards watchHazards = new();
	public List<CountyInfo> counties = [];
	public override string ToString()
	{
		string pds = string.Empty;
		if (watchHazards.isPDS)
			pds = "PDS ";
		return $"{pds}{watchType} {watchNumber} | Tornadoes: {watchHazards.tornadoes.hazard} ({watchHazards.tornadoes.chance}) | EF2+ Tornadoes: {watchHazards.ef2PlusTornadoes.hazard} ({watchHazards.ef2PlusTornadoes.chance}) | Severe Wind: {watchHazards.severeWind.hazard} ({watchHazards.severeWind.chance}) | 65 kt+ Wind: {watchHazards._65ktPlusWind.hazard} ({watchHazards._65ktPlusWind.chance}) | Severe Hail: {watchHazards.severeHail.hazard} ({watchHazards.severeHail.chance}) | 2\"+ Hail: {watchHazards._2InchPlusHail.hazard} ({watchHazards._2InchPlusHail.chance})";
	}
}

public record StormPredictionCenterWatchBox
{
	public string watchType = string.Empty;
	public string watchName = string.Empty;
	public int watchNumber = -1;
	public double maxHailSizeInches = -1;
	public double maxWindGustMph = -1;
	public bool isPDS = false;
	public double[] watchCenter = [];
	public DateTimeOffset issued = DateTimeOffset.MinValue;
	public DateTimeOffset expires = DateTimeOffset.MinValue;
	public SPCPolygon polygon = new();
	public override string ToString()
	{
		string pdsString = string.Empty;
		if (isPDS)
			pdsString = "PDS ";
		return $"{pdsString}{watchType} {watchNumber} | Max Hail Size: {Math.Floor(maxHailSizeInches * 100) / 100} in | Max Wind Gust: {Math.Floor(maxWindGustMph * 100) / 100} mph | Issued: {issued}";
	}
}