namespace Azrellie.Meteorology.SPC;

public record AuroraData
{
	public int Latitude { get; set; }
	public int Longitude { get; set; }
	public int Aurora { get; set; }

	public AuroraData(int lat, int lng, int aurora)
	{
		Latitude = lat;
		Longitude = lng;
		Aurora = aurora;
	}
}

public record SWPCAurora
{
	public DateTime ObservationTime { get; set; }
	public DateTime ForecastTime { get; set; }
	public List<AuroraData> Data { get; set; } = [];
}