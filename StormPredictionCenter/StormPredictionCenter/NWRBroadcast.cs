namespace Azrellie.Meteorology.SPC;

public class NWRBroadcast
{
	public string StateAbbreviation { get; set; }
	public string State { get; set; }
	public string County { get; set; }
	public string SAME { get; set; }
	public string SiteName { get; set; }
	public string SiteLocation { get; set; }
	public string SiteState { get; set; }
	public float Frequency { get; set; }
	public string Callsign { get; set; }
	public float Latitude { get; set; }
	public float Longitude { get; set; }
	public int PowerOutput { get; set; }
	public string Status { get; set; }
	public string WeatherForecastOffice { get; set; }
	public string Remarks { get; set; }
	public override string ToString() => $"{Callsign} ({Frequency}) - {State}, ({Latitude}, {Longitude}) | {Status}";
}