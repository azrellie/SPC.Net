using Newtonsoft.Json;

namespace Azrellie.Meteorology.SPC;

public record Address
{
	[JsonProperty("@type")]
	public string type { get; set; }
	public string streetAddress { get; set; }
	public string addressLocality { get; set; }
	public string addressRegion { get; set; }
	public string postalCode { get; set; }
}

public record ForecastOffice
{
	[JsonProperty("@type")]
	public string type { get; set; }
	public string id { get; set; }
	public string name { get; set; }
	public Address address { get; set; }
	public string telephone { get; set; }
	public string faxNumber { get; set; }
	public string email { get; set; }
	public string sameAs { get; set; }
	public string nwsRegion { get; set; }
	public string parentOrganization { get; set; }
	public List<string> responsibleCounties { get; set; }
	public List<string> responsibleForecastZones { get; set; }
	public List<string> responsibleFireZones { get; set; }
	public List<string> approvedObservationStations { get; set; }
}