using Newtonsoft.Json;

namespace Azrellie.Meteorology.SPC;

public record PointRef
{
	public string id { get; set; }
	public string type { get; set; }
	public Geometry geometry { get; set; }
	public Properties properties { get; set; }
}

public record AstronomicalData
{
	public DateTime sunrise { get; set; }
	public DateTime sunset { get; set; }
	public DateTime transit { get; set; }
	public DateTime civilTwilightBegin { get; set; }
	public DateTime civilTwilightEnd { get; set; }
	public DateTime nauticalTwilightBegin { get; set; }
	public DateTime nauticalTwilightEnd { get; set; }
	public DateTime astronomicalTwilightBegin { get; set; }
	public DateTime astronomicalTwilightEnd { get; set; }
}

public record Bearing
{
	public string unitCode { get; set; }
	public int value { get; set; }
}

public record Distance
{
	public string unitCode { get; set; }
	public double value { get; set; }
}

public record Nwr
{
	public string transmitter { get; set; }
	public string sameCode { get; set; }
	public string areaBroadcast { get; set; }
	public string pointBroadcast { get; set; }
}

public record Properties
{
	[JsonProperty("@id")]
	public string id { get; set; }
	public string cwa { get; set; }
	public string type { get; set; }
	public string forecastOffice { get; set; }
	public string gridId { get; set; }
	public int gridX { get; set; }
	public int gridY { get; set; }
	public string forecast { get; set; }
	public string forecastHourly { get; set; }
	public string forecastGridData { get; set; }
	public string observationStations { get; set; }
	public RelativeLocation relativeLocation { get; set; }
	public string forecastZone { get; set; }
	public string county { get; set; }
	public string fireWeatherZone { get; set; }
	public string timeZone { get; set; }
	public string radarStation { get; set; }
	public AstronomicalData astronomicalData { get; set; }
	public Nwr nwr { get; set; }
	public string city { get; set; }
	public string state { get; set; }
	public Distance distance { get; set; }
	public Bearing bearing { get; set; }
}

public record RelativeLocation
{
	public string type { get; set; }
	public Geometry geometry { get; set; }
	public Properties properties { get; set; }
}