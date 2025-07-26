using Newtonsoft.Json;
using System.Dynamic;

namespace Azrellie.Meteorology.SPC;

/// <summary>
/// Archive data that has been processed by this API by saving it to a file in GeoJSON format.
/// </summary>
/// <remarks>This will be updated soon in future versions to cover the new data that can be gathered.</remarks>
public class Archive(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	public void ArchiveData(object data, string extension)
	{
		if (data is List<RiskArea> outlookData)
		{
			GeoJson geoJson = new();
			foreach (RiskArea riskArea in outlookData)
			{
				List<List<List<double>>> polygons = [];
				foreach (SPCPolygon polygon in riskArea.polygons)
				{
					List<List<double>> _polygon = [];
					foreach (double[] coordinate in polygon.coordinates)
					{
						List<double> polygonCoordinate =
						[
							coordinate[0],
								coordinate[1]
						];
						_polygon.Add(polygonCoordinate);
					}
					polygons.Add(_polygon);
				}

				dynamic properties = new ExpandoObject();
				properties.riskType = riskArea.riskType;
				properties.valid = riskArea.valid;
				properties.expire = riskArea.expire;
				properties.issue = riskArea.issue;
				properties.label = riskArea.label;
				properties.label2 = riskArea.label2;
				properties.stroke = riskArea.stroke;
				properties.fill = riskArea.fill;

				SPCFeature feature = new();
				feature.geometry.type = "MultiPolygon";
				feature.geometry.coordinates.Add(polygons);
				feature.properties = properties;
				geoJson.features.Add(feature);
			}
			DateTime now = DateTime.Now;
			int day = outlookData[0].valid.Day - now.Day;
			File.WriteAllText(Environment.CurrentDirectory + $"\\spc_outlook_day{day + 1}_{now:MM-dd-yyyy-HHmmsszz}.{extension}", JsonConvert.SerializeObject(geoJson, Newtonsoft.Json.Formatting.Indented));
		}
	}
}