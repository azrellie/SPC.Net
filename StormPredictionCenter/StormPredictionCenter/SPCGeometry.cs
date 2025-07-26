using System.Dynamic;

namespace Azrellie.Meteorology.SPC;

public record SPCPolygon
{
	public List<double[]> coordinates = [];
	public List<List<double[]>> holes = [];
}
public record SPCPoint
{
	public double? lat;
	public double? lng;
	public SPCPoint(double lat, double lng)
	{
		this.lat = lat;
		this.lng = lng;
	}
	public SPCPoint() { }
}

public record Geometry
{
	public string type = "";
	public List<dynamic> coordinates = [];
}
public record SPCFeature
{
	public string type = "Feature";
	public Geometry geometry = new();
	public ExpandoObject properties = new();
}
public record GeoJson
{
	public string type = "FeatureCollection";
	public List<SPCFeature> features = [];
}