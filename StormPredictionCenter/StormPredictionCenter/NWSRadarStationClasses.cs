namespace Azrellie.Meteorology.SPC;

public record Elevation
{
	public string unit = string.Empty;
	public double elevation = 0;
}
public record RadarStation
{
	public SPCPoint location = new();
	public string id = string.Empty;
	public string name = string.Empty;
	public string stationType = string.Empty;
	public string timeZone = string.Empty;
	public string mode = string.Empty;
	public Elevation elevation = new();
}