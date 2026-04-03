namespace Azrellie.Meteorology.SPC;

public record Period
{
	public int number { get; set; }
	public string name { get; set; }
	public DateTime startTime { get; set; }
	public DateTime endTime { get; set; }
	public bool isDaytime { get; set; }
	public int temperature { get; set; }
	public string temperatureUnit { get; set; }
	public object temperatureTrend { get; set; }
	public ProbabilityOfPrecipitation probabilityOfPrecipitation { get; set; }
	public string windSpeed { get; set; }
	public string windDirection { get; set; }
	public string icon { get; set; }
	public string shortForecast { get; set; }
	public string detailedForecast { get; set; }
}

public record ForecastProperties
{
	public string units { get; set; }
	public string forecastGenerator { get; set; }
	public DateTime generatedAt { get; set; }
	public DateTime updateTime { get; set; }
	public string validTimes { get; set; }
	public Elevation elevation { get; set; }
	public List<Period> periods { get; set; }
}

public record ProbabilityOfPrecipitation
{
	public string unitCode { get; set; }
	public int value { get; set; }
}

public record Forecast
{
	public string type { get; set; }
	public Geometry geometry { get; set; }
	public ForecastProperties properties { get; set; }
}