using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using System.Globalization;
using System.Text.RegularExpressions;
using KMLFeature = SharpKml.Dom.Feature;
using KMLPolygon = SharpKml.Dom.Polygon;

namespace Azrellie.Meteorology.SPC;

/// <summary>
/// Retrieve certain outlooks from the SPC.
/// </summary>
public class Outlooks(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	/// <summary>
	/// Gets an archived categorical outlook from the Storm Prediction Center.
	/// </summary>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	/// <param name="year">The year of the outlook</param>
	/// <param name="month">The month of the outlook</param>
	/// <param name="day">The day of the categorical outlook</param>
	/// <param name="relativeDay">The day of the outlook relative to the day. Clamped between 1-3</param>
	/// <param name="time">The specific time of the outlook (2000, 1630, 1300, 1200, 0100)</param>
	public async Task<RiskArea[]> getCategoricalOutlook(int year, int month, int day, int relativeDay, OutlookTime time)
	{
		// day 0 wind outlook doesnt exist, it cant hurt you
		// day 0 wind outlook:
		if (relativeDay < 1 || relativeDay > 3)
			throw new InvalidSPCDayException($"'{relativeDay}' is not a valid SPC outlook day.");

		DateTime dt = new(year, month, day);
		if (new DateTime(2008, 5, 29) > dt)
			throw new InvalidSPCDateException($"The date {month}/{day}/{year} is an invalid date.");

		if (new DateTime(2014, 10, 22) > dt) // processing old categorical outlooks without mrgl and enh
		{
			List<RiskArea> spcObject = [];

			string strDay = day.ToString();
			if (day < 10)
				strDay = "0" + strDay;
			string strMonth = month.ToString();
			if (month < 10)
				strMonth = "0" + month;
			string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace($"Day{relativeDay}", string.Empty).Replace("Time", string.Empty)}.kmz";

			MemoryStream? kml = await Utils.processKmz(url);
			Parser activeKmzParser = new();
			activeKmzParser.Parse(kml, false);
			if (activeKmzParser.Root is Kml kmlFile)
			{
				List<Folder> folders = kmlFile.Flatten().OfType<Folder>().ToList();
				for (int i = 0; i < folders.Count; i++)
				{
					Folder folder = folders[i];

					if (!folder.Name.Contains("cat")) continue;
					List<KMLFeature> features = [..folder.Features];
					for (int j = 0; j < features.Count - 1; j++)
					{
						RiskArea risk = new();
						SPCPolygon spcPolygon = new();
						Placemark placemark = features[j] as Placemark;
						KMLPolygon polygon = placemark.Geometry as KMLPolygon;

						// check for cutout holes in the polygon
						if (features[j + 1] is Placemark nextPlacemark)
						{
							KMLPolygon nextPolygon = nextPlacemark.Geometry as KMLPolygon;
							List<double[]> hole = [];
							foreach (Vector coordinate in nextPolygon.OuterBoundary.LinearRing.Coordinates)
								hole.Add([coordinate.Latitude, coordinate.Longitude]);
							spcPolygon.holes.Add(hole);
						}

						foreach (Vector coordinate in polygon.OuterBoundary.LinearRing.Coordinates)
							spcPolygon.coordinates.Add([coordinate.Latitude, coordinate.Longitude]);

						switch (placemark.Name)
						{
							case "General Thunder":
								risk.label = "TSTM";
								break;
							case "Marginal Risk":
								risk.label = "MRGL";
								break;
							case "Slight Risk":
								risk.label = "SLGT";
								break;
							case "Enhanced Risk":
								risk.label = "ENH";
								break;
							case "Moderate Risk":
								risk.label = "MDT";
								break;
							case "High Risk":
								risk.label = "HIGH";
								break;
						}
						risk.label2 = placemark.Name;
						risk.polygons.Add(spcPolygon);
						spcObject.Add(risk);
					}
				}
			}
			return [..spcObject];
		}
		else // processing categorical outlooks with mrgl and enh added
		{
			string strDay = day.ToString();
			if (day < 10)
				strDay = "0" + strDay;
			string strMonth = month.ToString();
			if (month < 10)
				strMonth = "0" + month;
			string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace($"Day{relativeDay}", string.Empty).Replace("Time", string.Empty)}_cat.nolyr.geojson";

			string stringData = Utils.downloadString(url);

			List<RiskArea> spcObject = [];

			if (JsonConvert.DeserializeObject(stringData) is JObject parsedData)
				foreach (var feature in parsedData["features"])
				{
					// get data about the risk area
					RiskArea riskArea = new();
					CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
					int DN = (int)feature["properties"]["DN"];
					if (DN == 3)
						riskType = CategoricalRiskType.Marginal;
					else if (DN == 4)
						riskType = CategoricalRiskType.Slight;
					else if (DN == 5)
						riskType = CategoricalRiskType.Enhanced;
					else if (DN == 6)
						riskType = CategoricalRiskType.Moderate;
					else if (DN == 8)
						riskType = CategoricalRiskType.High;
					riskArea.riskType = riskType;

					// process valid date
					string valid = (string)feature["properties"]["VALID"];
					string yearValid = valid[0..4];
					string monthValid = valid[4..6];
					string dayValid = valid[6..8];
					string timeValid = valid[8..12];
					riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

					// process expire date
					string expire = (string)feature["properties"]["EXPIRE"];
					string yearExpire = expire[0..4];
					string monthExpire = expire[4..6];
					string dayExpire = expire[6..8];
					string timeExpire = expire[8..12];
					riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

					// process issue date
					string issue = (string)feature["properties"]["ISSUE"];
					string yearIssue = issue[0..4];
					string monthIssue = issue[4..6];
					string dayIssue = issue[6..8];
					string timeIssue = issue[8..12];
					riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

					riskArea.label = (string)feature["properties"]["LABEL"];
					riskArea.label2 = (string)feature["properties"]["LABEL2"];
					riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
					riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

					// iterate through the risk areas polygons/coordinates
					var polygons = feature["geometry"]["coordinates"];
					string type = (string)feature["geometry"]["type"];
					if (polygons != null)
						if (type == "MultiPolygon")
							foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
							{
								// polygon its self
								SPCPolygon polygonClass = new();
								foreach (var coordinate in polygon.First())
									polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

								// cutout holes
								for (int i = 1; i < polygon.Count(); i++)
								{
									List<double[]> holeList = [];
									foreach (var coordinate in polygon[i])
										holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
									polygonClass.holes.Add(holeList);
								}
								riskArea.polygons.Add(polygonClass);
							}
						else if (type == "Polygon")
						{
							// polygon its self
							SPCPolygon polygonClass = new();
							foreach (var coordinate in polygons.First())
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

							// cutout holes
							for (int i = 1; i < polygons.Count(); i++)
							{
								List<double[]> holeList = [];
								foreach (var coordinate in polygons[i])
									holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
								polygonClass.holes.Add(holeList);
							}
							riskArea.polygons.Add(polygonClass);
						}

					spcObject.Add(riskArea);
				}
			return [..spcObject];
		}
	}

	/// <summary>
	/// Gets the latest tornado outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the tornado outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the tornado outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getTornadoOutlook(int year, int month, int day, int relativeDay, OutlookTime time)
	{
		// day 0 tornado outlook doesnt exist, it cant hurt you
		// day 0 tornado outlook:
		if (relativeDay < 1 || relativeDay > 2)
			throw new InvalidSPCDayException($"'{relativeDay}' is not a valid SPC outlook day.");

		string strDay = day.ToString();
		if (day < 10)
			strDay = "0" + strDay;
		string strMonth = month.ToString();
		if (month < 10)
			strMonth = "0" + month;
		string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace($"Day{relativeDay}", string.Empty).Replace("Time", string.Empty)}_torn.nolyr.geojson";

		string stringData = await Utils.downloadStringAsync(url);
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			TornadoRisk riskType = TornadoRisk._2Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.02)
				riskType = TornadoRisk._2Percent;
			else if (DN == 0.05)
				riskType = TornadoRisk._5Percent;
			else if (DN == 0.1)
				riskType = TornadoRisk._10Percent;
			else if (DN == 0.15)
				riskType = TornadoRisk._15Percent;
			else if (DN == 0.3)
				riskType = TornadoRisk._30Percent;
			else if (DN == 0.45)
				riskType = TornadoRisk._45Percent;
			else if (DN == 0.6)
				riskType = TornadoRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}

		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest wind outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the wind outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the wind outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getWindOutlook(int year, int month, int day, int relativeDay, OutlookTime time)
	{
		// day 0 wind outlook doesnt exist, it cant hurt you
		// day 0 wind outlook:
		if (relativeDay < 1 || relativeDay > 2)
			throw new InvalidSPCDayException($"'{relativeDay}' is not a valid SPC outlook day.");

		string strDay = day.ToString();
		if (day < 10)
			strDay = "0" + strDay;
		string strMonth = month.ToString();
		if (month < 10)
			strMonth = "0" + month;
		string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace($"Day{relativeDay}", string.Empty).Replace("Time", string.Empty)}_wind.nolyr.geojson";

		string stringData = await Utils.downloadStringAsync(url);
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			WindHailRisk riskType = WindHailRisk._5Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.05)
				riskType = WindHailRisk._5Percent;
			else if (DN == 0.15)
				riskType = WindHailRisk._15Percent;
			else if (DN == 0.3)
				riskType = WindHailRisk._30Percent;
			else if (DN == 0.45)
				riskType = WindHailRisk._45Percent;
			else if (DN == 0.6)
				riskType = WindHailRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}
		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest hail outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the hail outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the hail outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getHailOutlook(int year, int month, int day, int relativeDay, OutlookTime time)
	{
		// day 0 hail outlook doesnt exist, it cant hurt you
		// day 0 hail outlook:
		if (relativeDay < 1 || relativeDay > 2)
			throw new InvalidSPCDayException($"'{relativeDay}' is not a valid SPC outlook day.");

		string strDay = day.ToString();
		if (day < 10)
			strDay = "0" + strDay;
		string strMonth = month.ToString();
		if (month < 10)
			strMonth = "0" + month;
		string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace($"Day{relativeDay}", string.Empty).Replace("Time", string.Empty)}_hail.nolyr.geojson";

		string stringData = await Utils.downloadStringAsync(url);
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			WindHailRisk riskType = WindHailRisk._5Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.05)
				riskType = WindHailRisk._5Percent;
			else if (DN == 0.15)
				riskType = WindHailRisk._15Percent;
			else if (DN == 0.3)
				riskType = WindHailRisk._30Percent;
			else if (DN == 0.45)
				riskType = WindHailRisk._45Percent;
			else if (DN == 0.6)
				riskType = WindHailRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}
		return [..spcObject];
	}

	/// <summary>
	/// Gets an archived categorical outlook from the Storm Prediction Center.
	/// <para><paramref name="day"/> parameter is clamped between 1-3, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	/// <param name="year">The year of the outlook</param>
	/// <param name="month">The month of the outlook</param>
	/// <param name="day">The day of the categorical outlook</param>
	/// <param name="getProbabilistic">Indicating whether the gathered data should be categorical or probablistic</param>
	public async Task<RiskArea[]> getCategoricalOutlookDay4Plus(int year, int month, int day)
	{
		string strDay = day.ToString();
		if (day < 10)
			strDay = "0" + strDay;
		string strMonth = month.ToString();
		if (month < 10)
			strMonth = "0" + month;

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_cat.nolyr.geojson");
		string stringData2 = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_prob.nolyr.geojson");
		string stringData3 = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_sigprob.nolyr.geojson");

		List<RiskArea> spcObject = [];

		// get categorical risks
		if (JsonConvert.DeserializeObject(stringData) is JObject parsedData)
			foreach (var feature in parsedData["features"])
			{
				// get data about the risk area
				RiskArea riskArea = new();
				CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
				int DN = (int)feature["properties"]["DN"];
				if (DN == 3)
					riskType = CategoricalRiskType.Marginal;
				else if (DN == 4)
					riskType = CategoricalRiskType.Slight;
				else if (DN == 5)
					riskType = CategoricalRiskType.Enhanced;
				else if (DN == 6)
					riskType = CategoricalRiskType.Moderate;
				else if (DN == 8)
					riskType = CategoricalRiskType.High;
				riskArea.riskType = riskType;

				// process valid date
				string valid = (string)feature["properties"]["VALID"];
				string yearValid = valid[0..4];
				string monthValid = valid[4..6];
				string dayValid = valid[6..8];
				string timeValid = valid[8..12];
				riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process expire date
				string expire = (string)feature["properties"]["EXPIRE"];
				string yearExpire = expire[0..4];
				string monthExpire = expire[4..6];
				string dayExpire = expire[6..8];
				string timeExpire = expire[8..12];
				riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process issue date
				string issue = (string)feature["properties"]["ISSUE"];
				string yearIssue = issue[0..4];
				string monthIssue = issue[4..6];
				string dayIssue = issue[6..8];
				string timeIssue = issue[8..12];
				riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				riskArea.label = (string)feature["properties"]["LABEL"];
				riskArea.label2 = (string)feature["properties"]["LABEL2"];
				riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
				riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

				// iterate through the risk areas polygons/coordinates
				var polygons = feature["geometry"]["coordinates"];
				string type = (string)feature["geometry"]["type"];
				if (polygons != null)
					if (type == "MultiPolygon")
						foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
						{
							// polygon its self
							SPCPolygon polygonClass = new();
							foreach (var coordinate in polygon.First())
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

							// cutout holes
							for (int i = 1; i < polygon.Count(); i++)
							{
								List<double[]> holeList = [];
								foreach (var coordinate in polygon[i])
									holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
								polygonClass.holes.Add(holeList);
							}
							riskArea.polygons.Add(polygonClass);
						}
					else if (type == "Polygon")
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygons.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygons.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygons[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}

				spcObject.Add(riskArea);
			}

		// get probabilistic risks
		if (JsonConvert.DeserializeObject(stringData2) is JObject parsedData2)
			foreach (var feature in parsedData2["features"])
			{
				// get data about the risk area
				RiskArea riskArea = new();
				CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
				int DN = (int)feature["properties"]["DN"];
				if (DN == 3)
					riskType = CategoricalRiskType.Marginal;
				else if (DN == 4)
					riskType = CategoricalRiskType.Slight;
				else if (DN == 5)
					riskType = CategoricalRiskType.Enhanced;
				else if (DN == 6)
					riskType = CategoricalRiskType.Moderate;
				else if (DN == 8)
					riskType = CategoricalRiskType.High;
				riskArea.riskType = riskType;

				// process valid date
				string valid = (string)feature["properties"]["VALID"];
				string yearValid = valid[0..4];
				string monthValid = valid[4..6];
				string dayValid = valid[6..8];
				string timeValid = valid[8..12];
				riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process expire date
				string expire = (string)feature["properties"]["EXPIRE"];
				string yearExpire = expire[0..4];
				string monthExpire = expire[4..6];
				string dayExpire = expire[6..8];
				string timeExpire = expire[8..12];
				riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process issue date
				string issue = (string)feature["properties"]["ISSUE"];
				string yearIssue = issue[0..4];
				string monthIssue = issue[4..6];
				string dayIssue = issue[6..8];
				string timeIssue = issue[8..12];
				riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				riskArea.label = (string)feature["properties"]["LABEL"];
				riskArea.label2 = (string)feature["properties"]["LABEL2"];
				riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
				riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

				// iterate through the risk areas polygons/coordinates
				var polygons = feature["geometry"]["coordinates"];
				string type = (string)feature["geometry"]["type"];
				if (polygons != null)
					if (type == "MultiPolygon")
						foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
						{
							// polygon its self
							SPCPolygon polygonClass = new();
							foreach (var coordinate in polygon.First())
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

							// cutout holes
							for (int i = 1; i < polygon.Count(); i++)
							{
								List<double[]> holeList = [];
								foreach (var coordinate in polygon[i])
									holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
								polygonClass.holes.Add(holeList);
							}
							riskArea.polygons.Add(polygonClass);
						}
					else if (type == "Polygon")
					{
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygons[0])
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						if (polygons.Count() > 1)
							foreach (var coordinate in polygons[1])
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
						riskArea.polygons.Add(polygonClass);
					}

				spcObject.Add(riskArea);
			}

		// get sig probailistic risks, if any
		if (JsonConvert.DeserializeObject(stringData3) is JObject parsedData3)
			foreach (var feature in parsedData3["features"])
			{
				// get data about the risk area
				RiskArea riskArea = new();
				CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
				int DN = (int)feature["properties"]["DN"];
				if (DN == 3)
					riskType = CategoricalRiskType.Marginal;
				else if (DN == 4)
					riskType = CategoricalRiskType.Slight;
				else if (DN == 5)
					riskType = CategoricalRiskType.Enhanced;
				else if (DN == 6)
					riskType = CategoricalRiskType.Moderate;
				else if (DN == 8)
					riskType = CategoricalRiskType.High;
				riskArea.riskType = riskType;

				// process valid date
				string valid = (string)feature["properties"]["VALID"];
				string yearValid = valid[0..4];
				string monthValid = valid[4..6];
				string dayValid = valid[6..8];
				string timeValid = valid[8..12];
				riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process expire date
				string expire = (string)feature["properties"]["EXPIRE"];
				string yearExpire = expire[0..4];
				string monthExpire = expire[4..6];
				string dayExpire = expire[6..8];
				string timeExpire = expire[8..12];
				riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process issue date
				string issue = (string)feature["properties"]["ISSUE"];
				string yearIssue = issue[0..4];
				string monthIssue = issue[4..6];
				string dayIssue = issue[6..8];
				string timeIssue = issue[8..12];
				riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				riskArea.label = (string)feature["properties"]["LABEL"];
				riskArea.label2 = (string)feature["properties"]["LABEL2"];
				riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
				riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

				// iterate through the risk areas polygons/coordinates
				var polygons = feature["geometry"]["coordinates"];
				string type = (string)feature["geometry"]["type"];
				if (polygons != null)
					if (type == "MultiPolygon")
						foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
						{
							// polygon its self
							SPCPolygon polygonClass = new();
							foreach (var coordinate in polygon.First())
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

							// cutout holes
							for (int i = 1; i < polygon.Count(); i++)
							{
								List<double[]> holeList = [];
								foreach (var coordinate in polygon[i])
									holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
								polygonClass.holes.Add(holeList);
							}
							riskArea.polygons.Add(polygonClass);
						}
					else if (type == "Polygon")
					{
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygons[0])
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						if (polygons.Count() > 1)
							foreach (var coordinate in polygons[1])
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
						riskArea.polygons.Add(polygonClass);
					}

				spcObject.Add(riskArea);
			}

		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest categorical outlook from the Storm Prediction Center.
	/// </summary>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getLatestCategoricalOutlook(int day = 1)
	{
		if (day < 1 || day > 3)
			throw new InvalidSPCDayException($"Day {day} is not a valid SPC outlook day.");

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/day{day}otlk_cat.nolyr.geojson");

		List<RiskArea> spcObject = [];

		if (JsonConvert.DeserializeObject(stringData) is JObject parsedData)
			foreach (var feature in parsedData["features"])
			{
				// get data about the risk area
				RiskArea riskArea = new();
				CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
				int DN = (int)feature["properties"]["DN"];
				if (DN == 3)
					riskType = CategoricalRiskType.Marginal;
				else if (DN == 4)
					riskType = CategoricalRiskType.Slight;
				else if (DN == 5)
					riskType = CategoricalRiskType.Enhanced;
				else if (DN == 6)
					riskType = CategoricalRiskType.Moderate;
				else if (DN == 8)
					riskType = CategoricalRiskType.High;
				riskArea.riskType = riskType;

				// process valid date
				string valid = (string)feature["properties"]["VALID"];
				string yearValid = valid[0..4];
				string monthValid = valid[4..6];
				string dayValid = valid[6..8];
				string timeValid = valid[8..12];
				riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process expire date
				string expire = (string)feature["properties"]["EXPIRE"];
				string yearExpire = expire[0..4];
				string monthExpire = expire[4..6];
				string dayExpire = expire[6..8];
				string timeExpire = expire[8..12];
				riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				// process issue date
				string issue = (string)feature["properties"]["ISSUE"];
				string yearIssue = issue[0..4];
				string monthIssue = issue[4..6];
				string dayIssue = issue[6..8];
				string timeIssue = issue[8..12];
				riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

				riskArea.label = (string)feature["properties"]["LABEL"];
				riskArea.label2 = (string)feature["properties"]["LABEL2"];
				riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
				riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

				// iterate through the risk areas polygons/coordinates
				var polygons = feature["geometry"]["coordinates"];
				string type = (string)feature["geometry"]["type"];
				if (polygons != null)
					if (type == "MultiPolygon")
						foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
						{
							// polygon its self
							SPCPolygon polygonClass = new();
							foreach (var coordinate in polygon.First())
								polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

							// cutout holes
							for (int i = 1; i < polygon.Count(); i++)
							{
								List<double[]> holeList = [];
								foreach (var coordinate in polygon[i])
									holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
								polygonClass.holes.Add(holeList);
							}
							riskArea.polygons.Add(polygonClass);
						}
					else if (type == "Polygon")
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygons.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygons.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygons[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}

				spcObject.Add(riskArea);
			}
		return [..spcObject];
	}

	/// <summary>
	/// Similar to <see cref="getLatestCategoricalOutlook()"/>, but is specifically for getting the latest data from categorical outlooks days 4 to 8.
	/// <para><paramref name="day"/> parameter is clamped between 4-8, and defaults to 4 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the categorical outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getLatestCategoricalOutlookDay4Plus(int day = 4)
	{
		// day 0 categorical outlook doesnt exist, it cant hurt you
		// day 0 categorical risk:
		if (day < 4 || day > 8)
			throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/exper/day4-8/day{day}prob.nolyr.geojson");
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			CategoricalRiskType riskType = CategoricalRiskType.GeneralThunderstorms;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 3)
				riskType = CategoricalRiskType.Marginal;
			else if (DN == 4)
				riskType = CategoricalRiskType.Slight;
			else if (DN == 5)
				riskType = CategoricalRiskType.Enhanced;
			else if (DN == 6)
				riskType = CategoricalRiskType.Moderate;
			else if (DN == 8)
				riskType = CategoricalRiskType.High;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..12];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..12];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..12];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HHmm", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}
		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest tornado outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the tornado outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the tornado outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getLatestTornadoOutlook(int day = 1)
	{
		// day 0 tornado outlook doesnt exist, it cant hurt you
		// day 0 tornado outlook:
		if (day < 1 || day > 2)
			throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/day{day}otlk_torn.nolyr.geojson");
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			TornadoRisk riskType = TornadoRisk._2Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.02)
				riskType = TornadoRisk._2Percent;
			else if (DN == 0.05)
				riskType = TornadoRisk._5Percent;
			else if (DN == 0.1)
				riskType = TornadoRisk._10Percent;
			else if (DN == 0.15)
				riskType = TornadoRisk._15Percent;
			else if (DN == 0.3)
				riskType = TornadoRisk._30Percent;
			else if (DN == 0.45)
				riskType = TornadoRisk._45Percent;
			else if (DN == 0.6)
				riskType = TornadoRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}

		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest wind outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the wind outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the wind outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getLatestWindOutlook(int day = 1)
	{
		// day 0 wind outlook doesnt exist, it cant hurt you
		// day 0 wind outlook:
		if (day < 1 || day > 2)
			throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/day{day}otlk_wind.nolyr.geojson");
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			WindHailRisk riskType = WindHailRisk._5Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.05)
				riskType = WindHailRisk._5Percent;
			else if (DN == 0.15)
				riskType = WindHailRisk._15Percent;
			else if (DN == 0.3)
				riskType = WindHailRisk._30Percent;
			else if (DN == 0.45)
				riskType = WindHailRisk._45Percent;
			else if (DN == 0.6)
				riskType = WindHailRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}
		return [..spcObject];
	}

	/// <summary>
	/// Gets the latest hail outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the hail outlook that should be retrieved.
	/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
	/// </summary>
	/// <param name="day">The day of the hail outlook.</param>
	/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
	/// <exception cref="InvalidSPCDayException"/>
	/// <exception cref="SPCOutlookDoesntExistException"/>
	public async Task<RiskArea[]> getLatestHailOutlook(int day = 1)
	{
		// day 0 hail outlook doesnt exist, it cant hurt you
		// day 0 hail outlook:
		if (day < 1 || day > 2)
			throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

		string stringData = await Utils.downloadStringAsync($"https://www.spc.noaa.gov/products/outlook/day{day}otlk_hail.nolyr.geojson");
		JObject? parsedData = JsonConvert.DeserializeObject(stringData) as JObject;

		List<RiskArea> spcObject = [];

		foreach (var feature in parsedData["features"])
		{
			// get data about the risk area
			RiskArea riskArea = new();

			WindHailRisk riskType = WindHailRisk._5Percent;
			int DN = (int)feature["properties"]["DN"];
			if (DN == 0.05)
				riskType = WindHailRisk._5Percent;
			else if (DN == 0.15)
				riskType = WindHailRisk._15Percent;
			else if (DN == 0.3)
				riskType = WindHailRisk._30Percent;
			else if (DN == 0.45)
				riskType = WindHailRisk._45Percent;
			else if (DN == 0.6)
				riskType = WindHailRisk._60Percent;
			riskArea.riskType = riskType;

			// process valid date
			string valid = (string)feature["properties"]["VALID"];
			string yearValid = valid[0..4];
			string monthValid = valid[4..6];
			string dayValid = valid[6..8];
			string timeValid = valid[8..10];
			riskArea.valid = DateTime.ParseExact($"{yearValid} {monthValid} {dayValid} {timeValid}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process expire date
			string expire = (string)feature["properties"]["EXPIRE"];
			string yearExpire = expire[0..4];
			string monthExpire = expire[4..6];
			string dayExpire = expire[6..8];
			string timeExpire = expire[8..10];
			riskArea.expire = DateTime.ParseExact($"{yearExpire} {monthExpire} {dayExpire} {timeExpire}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			// process issue date
			string issue = (string)feature["properties"]["ISSUE"];
			string yearIssue = issue[0..4];
			string monthIssue = issue[4..6];
			string dayIssue = issue[6..8];
			string timeIssue = issue[8..10];
			riskArea.issue = DateTime.ParseExact($"{yearIssue} {monthIssue} {dayIssue} {timeIssue}", "yyyy MM dd HH", CultureInfo.InvariantCulture);

			riskArea.label = (string)feature["properties"]["LABEL"];
			riskArea.label2 = (string)feature["properties"]["LABEL2"];
			riskArea.stroke = Utils.hexToRgb((string)feature["properties"]["stroke"]);
			riskArea.fill = Utils.hexToRgb((string)feature["properties"]["fill"]);
			riskArea.isSignificant = riskArea.label == "SIGN";

			// iterate through the risk areas polygons/coordinates
			var polygons = feature["geometry"]["coordinates"];
			string type = (string)feature["geometry"]["type"];
			if (polygons != null)
				if (type == "MultiPolygon")
					foreach (var polygon in polygons) // this is the entire polygon, including its self and cutout holes
					{
						// polygon its self
						SPCPolygon polygonClass = new();
						foreach (var coordinate in polygon.First())
							polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

						// cutout holes
						for (int i = 1; i < polygon.Count(); i++)
						{
							List<double[]> holeList = [];
							foreach (var coordinate in polygon[i])
								holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
							polygonClass.holes.Add(holeList);
						}
						riskArea.polygons.Add(polygonClass);
					}
				else if (type == "Polygon")
				{
					// polygon its self
					SPCPolygon polygonClass = new();
					foreach (var coordinate in polygons.First())
						polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

					// cutout holes
					for (int i = 1; i < polygons.Count(); i++)
					{
						List<double[]> holeList = [];
						foreach (var coordinate in polygons[i])
							holeList.Add([(double)coordinate[0], (double)coordinate[1]]);
						polygonClass.holes.Add(holeList);
					}
					riskArea.polygons.Add(polygonClass);
				}

			spcObject.Add(riskArea);
		}
		return [.. spcObject];
	}

	/// <summary>
	/// Gets the latest mesoscale discussions from the Storm Prediciton Center.
	/// </summary>
	/// <returns>An array of <see cref="StormPredictionCenterMesoscaleDiscussion"/> objects which contains the processed data in a more easy to use format.</returns>
	public async Task<StormPredictionCenterMesoscaleDiscussion[]> getLatestMesoscaleDiscussion()
	{
		// note that archived mds can be found via http://www.spc.noaa.gov/products/md/MD2309.kmz
		Dictionary<string, string> timeZoneConvert = new()
		{
			{"AST", "-04:00"},
			{"ADT", "-03:00"},
			{"GMT", "+00:00"},
			{"EST", "-05:00"},
			{"EDT", "-04:00"},
			{"CST", "-06:00"},
			{"CDT", "-05:00"},
			{"MST", "-07:00"},
			{"MDT", "-06:00"},
			{"AZOT", "-01:00"},
			{"AZOST", "+00:00"},
			{"CVT", "-01:00"},
			{"PDT", "-07:00"},
			{"PST", "-08:00"}
		};

		MemoryStream? kml = await Utils.processKmz("https://www.spc.noaa.gov/products/md/ActiveMD.kmz");
		List<MemoryStream> mds = [];

		// extract active mesoscale discussion kmz files and store it in active md folder
		Parser activeKmzParser = new();
		activeKmzParser.Parse(kml, false);

		List<StormPredictionCenterWatchBox> stormPredictionCenterWatchBoxes = [];
		if (activeKmzParser.Root is Kml activeMdKmz)
		{
			var downloadTasks = activeMdKmz.Flatten().OfType<Folder>().SelectMany(folder => folder.Flatten().OfType<Link>().Select(link => link.Href)).Select(async activeMd => mds.Add(await Utils.processKmz(activeMd.KmzUrl().AbsoluteUri)));
			await Task.WhenAll(downloadTasks);
		}

		// begin reading the md kml files
		List<StormPredictionCenterMesoscaleDiscussion> stormPredictionCenterMesoscaleDiscussions = [];
		foreach (MemoryStream mesoscaleDiscussion in mds)
		{
			StormPredictionCenterMesoscaleDiscussion stormPredictionCenterMesoscaleDiscussion = new();
			Parser parser = new();
			parser.Parse(mesoscaleDiscussion, false);
			if (parser.Root is Kml mdKml)
				foreach (var feature in mdKml.Flatten().OfType<Placemark>())
				{
					SPCPolygon polygon = new();
					KMLPolygon geometry = feature.Geometry as KMLPolygon;
					foreach (Vector vector in geometry.OuterBoundary.LinearRing.Coordinates)
						polygon.coordinates.Add([vector.Latitude, vector.Longitude]);
					stormPredictionCenterMesoscaleDiscussion.polygon = polygon;
					string[] lines = Regex.Replace(feature.Description.Text, "<.*?>", string.Empty).Trim().Split('\n');
					stormPredictionCenterMesoscaleDiscussion.url = lines[0].Split(' ')[1].Trim();
					stormPredictionCenterMesoscaleDiscussion.fullName = lines[1].Trim();
					stormPredictionCenterMesoscaleDiscussion.mesoscaleNumber = int.Parse(lines[1].Split(' ')[2]);
					string issuedString = lines[2].Split(':')[1];
					string timeZone = string.Empty;
					string timeOffset = string.Empty;
					foreach (KeyValuePair<string, string> pair in timeZoneConvert)
						if (issuedString.Contains(pair.Key))
						{
							timeOffset = pair.Value;
							timeZone = pair.Key;
						}
					DateTimeOffset issued = DateTimeOffset.ParseExact(issuedString.Replace(timeZone, timeOffset).Trim(), "hhmm tt zzz ddd MMM dd yyyy", CultureInfo.InvariantCulture);
					stormPredictionCenterMesoscaleDiscussion.issued = issued;
					stormPredictionCenterMesoscaleDiscussion.issuedString = issued.ToString("MM-dd-yyyy hh:mm tt TZ").Replace("TZ", timeZone).Trim();
					stormPredictionCenterMesoscaleDiscussion.areasAffected = lines[3].Trim();
					string type = string.Empty;
					if (lines.Length >= 5)
						if (lines[4].Contains("severe potential", StringComparison.InvariantCultureIgnoreCase))
							type = "Severe Potential";
						else if (lines[4].Contains("tornado watch", StringComparison.InvariantCultureIgnoreCase))
							type = "Concerning Tornado Watch " + Regex.Replace(lines[4], "[^0-9]", string.Empty);
						else if (lines[4].Contains("severe thunderstorm watch", StringComparison.InvariantCultureIgnoreCase))
							type = "Concerning Severe Thunderstorm Watch " + Regex.Replace(lines[4], "[^0-9.]", string.Empty);
						else if (lines[4].Contains("snow", StringComparison.InvariantCultureIgnoreCase))
							type = "Heavy Snow";
						else if (lines[4].Contains("freezing rain", StringComparison.InvariantCultureIgnoreCase))
							type = "Freezing Rain";
						else if (lines[4].Contains("blizzard", StringComparison.InvariantCultureIgnoreCase))
							type = "Blizzard";
					stormPredictionCenterMesoscaleDiscussion.type = type;
				}
			stormPredictionCenterMesoscaleDiscussions.Add(stormPredictionCenterMesoscaleDiscussion);
		}

		return [..stormPredictionCenterMesoscaleDiscussions];
	}
}