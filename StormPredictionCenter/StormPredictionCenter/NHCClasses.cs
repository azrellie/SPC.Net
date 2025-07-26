namespace Azrellie.Meteorology.SPC;

public record WindRadii
{
	public int windSpeedKts { get; set; } = 0;
	public List<double[]> coordinates { get; set; } = [];
}
public record ForecastCone
{
	public string timezone { get; set; } = string.Empty;
	public string type { get; set; } = string.Empty;
	public DateTime advisoryDate { get; set; } = DateTime.UnixEpoch;
	public string basin { get; set; } = string.Empty;
	public string advisoryNumber { get; set; } = string.Empty;
	public int stormNumber { get; set; } = 0;
	public string stormName { get; set; } = string.Empty;
	public List<double[]> coordinates { get; set; } = [];
}
public record ForecastPoint
{
	public double latitude = 0;
	public double longitude = 0;
	public string stormName = string.Empty;
	public int stormNumber = 0;
	public string basin = string.Empty;
	public string type = string.Empty;
	public int intensityKts = 0;
	public int intensityMph = 0;
	public int intensityKmh = 0;
	public int windGustsKts = 0;
	public int windGustsMph = 0;
	public int minSeaLevelPressure = 0;
	public string forecastHour = string.Empty;
	public DateTime date = DateTime.UnixEpoch;
}
public record StormObject
{
	public string type { get; set; } = string.Empty;
	public string name { get; set; } = string.Empty;
	public string wallet { get; set; } = string.Empty;
	public double centerLat { get; set; }
	public double centerLng { get; set; }
	public DateTime dateTime { get; set; }
	public string movement { get; set; } = string.Empty;
	public int minimumPressureMbar { get; set; }
	public int maxSustainedWindsMph { get; set; }
	public string headline { get; set; } = string.Empty;
	public List<ForecastPoint> forecastPoints { get; set; } = [];
	public List<ForecastPoint> pastTrack { get; set; } = [];
	public ForecastCone forecastCone { get; set; } = new();
	public List<WindRadii> WindRadii { get; set; } = [];
}

public record DisturbanceObject
{
	public SPCPolygon polygon { get; set; } = new();
	public SPCPoint point { get; set; } = new();
	public byte disturbanceIndex { get; set; } = 0;
	public string day2Percentage { get; set; } = string.Empty;
	public string day2Category { get; set; } = string.Empty;
	public string day7Percentage { get; set; } = string.Empty;
	public string day7Category { get; set; } = string.Empty;
	public string discussion { get; set; } = string.Empty;
	public override string ToString() => $"Disturbance {disturbanceIndex} - {day2Percentage} chance of cyclone formation in 48 hours - {day7Percentage} chance of cyclone formation in 7 days";
}