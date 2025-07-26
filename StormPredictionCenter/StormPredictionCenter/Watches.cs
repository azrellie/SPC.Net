using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SharpKml.Base;
using SharpKml.Dom;
using System.Globalization;
using SharpKml.Engine;
using System.Collections.Concurrent;
using KMLPolygon = SharpKml.Dom.Polygon;

namespace Azrellie.Meteorology.SPC;

// warnings be gone
#pragma warning disable CS8600
#pragma warning disable CS8601
#pragma warning disable CS8602
#pragma warning disable CS8604

/// <summary>
/// Retrieve certain watches from the SPC.
/// </summary>
public class Watches(StormPredictionCenter? self)
{
	private readonly StormPredictionCenter? parent = self;

	/// <summary>
	/// Gets currently active severe thunderstorm watches from the National Weather Service.
	/// </summary>
	/// <returns>A <see cref="StormPredictionCenterWatch"/> class object which contains the processed data in a more easy to use format.</returns>
	public async Task<StormPredictionCenterWatch[]> getActiveSevereThunderstormWatches()
	{
		Dictionary<int, StormPredictionCenterWatch> stormPredictionCenterWatches = [];

		// download the main json data as a string
		string jsonString = await Utils.downloadStringAsync("https://api.weather.gov/alerts/active?event=severe%20thunderstorm%20watch");
		if (JsonConvert.DeserializeObject(jsonString) is not JObject jsonData)
			return [];

		var tasks = jsonData["features"]?.Select(async alert =>
		{
			string description = (string)alert["properties"]["description"];
			int watchNumber = Utils.getSevereThunderstormWatchNumber(description);
			StormPredictionCenterWatch stormPredictionCenterWatch = new();

			var countyTasks = ((JArray)alert["properties"]["affectedZones"]).Select(async countyAffected =>
			{
				if (countyAffected == null) return;

				// download county data
				string countyJson = await Utils.downloadStringAsync((string)countyAffected);
				if (string.IsNullOrEmpty(countyJson)) return;
				parent?.debugLog("Downloading county data " + countyAffected);

				JObject? county = JsonConvert.DeserializeObject(countyJson) as JObject;
				if (county?["geometry"] == null) return;

				string geometryType = (string)county["geometry"]["type"];
				var polygonCoordinates = county["geometry"]["coordinates"];
				CountyInfo countyInfo = new();
				SPCPolygon polygon = new();

				// process polygon or multipolygon
				if (geometryType == "Polygon")
					foreach (var coordinate in polygonCoordinates[0])
						polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
				else if (geometryType == "MultiPolygon")
					foreach (var multiPolygon in polygonCoordinates)
						foreach (var subPolygon in multiPolygon)
							foreach (var coordinate in subPolygon)
								polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

				// populate county info
				countyInfo.id = (string)county["properties"]["id"];
				countyInfo.name = (string)county["properties"]["name"];
				countyInfo.state = (string)county["properties"]["state"];
				foreach (string forecastOffice in county["properties"]["forecastOffices"])
					countyInfo.forecastOffices.Add(forecastOffice);
				countyInfo.timeZone = (string)county["properties"]["timeZone"][0];
				countyInfo.geometry = polygon;

				// add county info to the watch
				lock (stormPredictionCenterWatch)
				{
					stormPredictionCenterWatch.counties.Add(countyInfo);
				}
			});

			await Task.WhenAll(countyTasks);

			// populate watch details
			stormPredictionCenterWatch.sent = DateTime.Parse((string)alert["properties"]["sent"]);
			stormPredictionCenterWatch.effective = DateTime.Parse((string)alert["properties"]["effective"]);
			stormPredictionCenterWatch.onset = DateTime.Parse((string)alert["properties"]["onset"]);
			stormPredictionCenterWatch.expires = DateTime.Parse((string)alert["properties"]["expires"]);
			stormPredictionCenterWatch.ends = DateTime.Parse((string)alert["properties"]["ends"]);
			stormPredictionCenterWatch.sender = (string)alert["properties"]["senderName"];
			stormPredictionCenterWatch.headline = (string)alert["properties"]["headline"];
			stormPredictionCenterWatch.description = description;
			string eventType = (string)alert["properties"]["messageType"];
			if (eventType == "Update")
				stormPredictionCenterWatch.status = WarningEventType.Update;
			else if (eventType == "Alert")
				stormPredictionCenterWatch.status = WarningEventType.NewIssue;
			else if (eventType == "Cancel")
				stormPredictionCenterWatch.status = WarningEventType.Cancel;
			else if (eventType == "Ack")
				stormPredictionCenterWatch.status = WarningEventType.Acknowledge;
			else if (eventType == "Error")
				stormPredictionCenterWatch.status = WarningEventType.Error;
			stormPredictionCenterWatch.watchNumber = watchNumber;
			stormPredictionCenterWatch.watchType = "Severe Thunderstorm Watch";
			stormPredictionCenterWatch.watchHazards = await getWatchRisks(watchNumber, DateTime.UtcNow.Year);

			lock (stormPredictionCenterWatches)
			{
				// if the add fails, then merge the county data with the existing watch
				if (!stormPredictionCenterWatches.TryAdd(watchNumber, stormPredictionCenterWatch))
					foreach (CountyInfo countyInfo in stormPredictionCenterWatch.counties)
						stormPredictionCenterWatches[watchNumber].counties.Add(countyInfo);
			}
		});

		if (tasks != null)
			await Task.WhenAll(tasks);

		foreach (var watch in stormPredictionCenterWatches)
		{
			double[] center = new double[2];
			int pointCount = 0;
			foreach (var county in watch.Value.counties)
				foreach (double[] point in county.geometry.coordinates)
				{
					center[0] += point[1];
					center[1] += point[0];
					pointCount++;
				}
			center[0] /= pointCount;
			center[1] /= pointCount;
			watch.Value.watchCenter = center;
		}

		return [..stormPredictionCenterWatches.Values];
	}

	/// <summary>
	/// Gets currently active tornado watches from the National Weather Service.
	/// </summary>
	/// <returns>A <see cref="StormPredictionCenterWatch"/> class object which contains the processed data in a more easy to use format.</returns>
	public async Task<StormPredictionCenterWatch[]> getActiveTornadoWatches()
	{
		Dictionary<int, StormPredictionCenterWatch> stormPredictionCenterWatches = [];

		string jsonString = await Utils.downloadStringAsync("https://api.weather.gov/alerts/active?event=tornado%20watch");
		if (JsonConvert.DeserializeObject(jsonString) is not JObject jsonData)
			return [];

		var tasks = jsonData["features"]?.Select(async alert =>
		{
			string description = (string)alert["properties"]["description"];
			int watchNumber = Utils.getTornadoWatchNumber(description);
			StormPredictionCenterWatch stormPredictionCenterWatch = new();

			var countyTasks = ((JArray)alert["properties"]["affectedZones"]).Select(async countyAffected =>
			{
				if (countyAffected == null) return;

				// download county data
				string countyJson = await Utils.downloadStringAsync((string)countyAffected);
				if (string.IsNullOrEmpty(countyJson)) return;
				parent?.debugLog("Downloading county data " + countyAffected);

				JObject? county = JsonConvert.DeserializeObject(countyJson) as JObject;
				if (county?["geometry"] == null) return;

				string geometryType = (string)county["geometry"]["type"];
				var polygonCoordinates = county["geometry"]["coordinates"];
				CountyInfo countyInfo = new();
				SPCPolygon polygon = new();

				// process polygon or multipolygon
				if (geometryType == "Polygon")
					foreach (var coordinate in polygonCoordinates[0])
						polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
				else if (geometryType == "MultiPolygon")
					foreach (var multiPolygon in polygonCoordinates)
						foreach (var subPolygon in multiPolygon)
							foreach (var coordinate in subPolygon)
								polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

				// populate county info
				countyInfo.id = (string)county["properties"]["id"];
				countyInfo.name = (string)county["properties"]["name"];
				countyInfo.state = (string)county["properties"]["state"];
				foreach (string forecastOffice in county["properties"]["forecastOffices"])
					countyInfo.forecastOffices.Add(forecastOffice);
				countyInfo.timeZone = (string)county["properties"]["timeZone"][0];
				countyInfo.geometry = polygon;

				// add county info to the watch
				lock (stormPredictionCenterWatch)
				{
					stormPredictionCenterWatch.counties.Add(countyInfo);
				}
			});

			await Task.WhenAll(countyTasks);

			// populate watch details
			stormPredictionCenterWatch.sent = DateTime.Parse((string)alert["properties"]["sent"]);
			stormPredictionCenterWatch.effective = DateTime.Parse((string)alert["properties"]["effective"]);
			stormPredictionCenterWatch.onset = DateTime.Parse((string)alert["properties"]["onset"]);
			stormPredictionCenterWatch.expires = DateTime.Parse((string)alert["properties"]["expires"]);
			stormPredictionCenterWatch.ends = DateTime.Parse((string)alert["properties"]["ends"]);
			stormPredictionCenterWatch.sender = (string)alert["properties"]["senderName"];
			stormPredictionCenterWatch.headline = (string)alert["properties"]["headline"];
			stormPredictionCenterWatch.description = description;
			string eventType = (string)alert["properties"]["messageType"];
			if (eventType == "Update")
				stormPredictionCenterWatch.status = WarningEventType.Update;
			else if (eventType == "Alert")
				stormPredictionCenterWatch.status = WarningEventType.NewIssue;
			else if (eventType == "Cancel")
				stormPredictionCenterWatch.status = WarningEventType.Cancel;
			else if (eventType == "Ack")
				stormPredictionCenterWatch.status = WarningEventType.Acknowledge;
			else if (eventType == "Error")
				stormPredictionCenterWatch.status = WarningEventType.Error;
			stormPredictionCenterWatch.watchNumber = watchNumber;
			stormPredictionCenterWatch.watchType = "Tornado Watch";
			stormPredictionCenterWatch.watchHazards = await getWatchRisks(watchNumber, DateTime.UtcNow.Year);

			lock (stormPredictionCenterWatches)
			{
				// if the add fails, then merge the county data with the existing watch
				if (!stormPredictionCenterWatches.TryAdd(watchNumber, stormPredictionCenterWatch))
					foreach (CountyInfo countyInfo in stormPredictionCenterWatch.counties)
						stormPredictionCenterWatches[watchNumber].counties.Add(countyInfo);
			}
		});

		if (tasks != null)
			await Task.WhenAll(tasks);

		foreach (var watch in stormPredictionCenterWatches)
		{
			double[] center = new double[2];
			int pointCount = 0;
			foreach (var county in watch.Value.counties)
				foreach (double[] point in county.geometry.coordinates)
				{
					center[0] += point[1];
					center[1] += point[0];
					pointCount++;
				}
			center[0] /= pointCount;
			center[1] /= pointCount;
			watch.Value.watchCenter = center;
		}

		return [..stormPredictionCenterWatches.Values];
	}

	/// <summary>
	/// Gets watch boxes for currently active severe thunderstorm/tornado watches from the Storm Prediction Center.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWatchBox"/> class which contains the processed data in a more easy to use format.</returns>
	public async Task<StormPredictionCenterWatchBox[]> getActiveWatchBoxes()
	{
		MemoryStream? kml = await Utils.processKmz("https://www.spc.noaa.gov/products/watch/ActiveWW.kmz");
		if (kml == null) return [];
		List<MemoryStream> watches = [];

		// parse the kml file
		Parser activeKmzParser = new();
		activeKmzParser.Parse(kml, false);

		List<StormPredictionCenterWatchBox> stormPredictionCenterWatchBoxes = [];
		if (activeKmzParser.Root is Kml activeMdKmz)
		{
			var downloadTasks = activeMdKmz.Flatten().OfType<Folder>().SelectMany(folder => folder.Flatten().OfType<Link>().Select(link => link.Href)).Select(async activeMd => watches.Add(await Utils.processKmz(activeMd.KmzUrl().AbsoluteUri)));
			await Task.WhenAll(downloadTasks);
		}

		foreach (MemoryStream watchBox in watches)
		{
			if (watchBox == null) continue;
			StormPredictionCenterWatchBox stormPredictionCenterWatchBox = new();
			Parser parser = new();
			parser.Parse(watchBox, false);

			if (parser.Root is Kml mdKml)
				foreach (var feature in mdKml.Flatten().OfType<Placemark>())
				{
					SPCPolygon polygon = new();
					if (feature.Geometry is KMLPolygon geometry)
						foreach (Vector vector in geometry.OuterBoundary.LinearRing.Coordinates)
							polygon.coordinates.Add([vector.Latitude, vector.Longitude]);

					string[] split = feature.Name.Split(' ');
					string time = string.Empty;
					foreach (string str in split)
						if (DateTime.TryParseExact(str, "ddHHmm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
							time = str;

					DateTimeOffset issued = DateTimeOffset.ParseExact(DateTime.UtcNow.ToString("yyyyMM") + time, "yyyyMMddHHmm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

					stormPredictionCenterWatchBox.issued = issued;
					stormPredictionCenterWatchBox.polygon = polygon;
					stormPredictionCenterWatchBox.watchCenter = [polygon.coordinates.Average(x => x[1]), polygon.coordinates.Average(x => x[0])];
					stormPredictionCenterWatchBox.watchName = feature.Name;
					stormPredictionCenterWatchBox.watchNumber = int.Parse(feature.Name[2..6]);
					stormPredictionCenterWatchBox.isPDS = feature.Name.Contains("pds", StringComparison.CurrentCultureIgnoreCase) || feature.Name.Contains("particularly dangerous situation", StringComparison.CurrentCultureIgnoreCase);
					stormPredictionCenterWatchBox.watchType = feature.StyleUrl.ToString().Trim('#');
				}

			stormPredictionCenterWatchBoxes.Add(stormPredictionCenterWatchBox);
		}

		return [..stormPredictionCenterWatchBoxes];
	}

	/// <summary>
	/// Gets archived tornado and severe thunderstorm watches from the National Weather Service and Iowa Environmental Mesonet.
	/// </summary>
	/// <returns>An array of <see cref="StormPredictionCenterWatch"/> class which contains the processed data in a more easy to use format.</returns>
	public async Task<StormPredictionCenterWatchBox[]> getArchivedWatches(int year, int month, int day, string time = "")
	{
		ConcurrentBag<StormPredictionCenterWatchBox> stormPredictionCenterWatchBoxes = [];
		string strMonth = month.ToString("D2");
		string strDay = day.ToString("D2");

		if (!string.IsNullOrEmpty(time))
		{
			string url = $"https://mesonet.agron.iastate.edu/json/spcwatch.py?ts={year}{strMonth}{strDay}{time}&fmt=geojson";
			string jsonString = await Utils.downloadStringAsync(url);

			if (JsonConvert.DeserializeObject(jsonString) is JObject jsonData)
				foreach (var watch in jsonData["features"])
					stormPredictionCenterWatchBoxes.Add(parseWatch(watch));
		}
		else
		{
			var tasks = Enumerable.Range(0, 24).Select(async hour =>
			{
				string url = $"https://mesonet.agron.iastate.edu/json/spcwatch.py?ts={year}{strMonth}{strDay}{hour * 100:0000}&fmt=geojson";
				string jsonString = await Utils.downloadStringAsync(url);

				if (JsonConvert.DeserializeObject(jsonString) is JObject jsonData)
					foreach (var watch in jsonData["features"])
						stormPredictionCenterWatchBoxes.Add(parseWatch(watch));
			});

			await Task.WhenAll(tasks);
		}

		// remove any duplicates
		return [..stormPredictionCenterWatchBoxes.Distinct(new WatchBoxComparer())];
	}

	private StormPredictionCenterWatchBox parseWatch(JToken watch)
	{
		StormPredictionCenterWatchBox watchBox = new();
		string watchType = (string)watch["properties"]["type"];
		int watchNumber = (int)watch["properties"]["number"];
		bool isPDS = (bool)watch["properties"]["is_pds"];
		string pdsTag = isPDS ? "PDS " : string.Empty;

		watchBox.watchName = watchType == "TOR" ? $"{pdsTag}Tornado Watch {watchNumber}" : $"{pdsTag}Severe Thunderstorm Watch {watchNumber}";

		watchBox.watchType = watchType;
		watchBox.watchNumber = watchNumber;
		watchBox.maxHailSizeInches = (double)watch["properties"]["max_hail_size"];
		watchBox.maxWindGustMph = (double)watch["properties"]["max_wind_gust_knots"] * 1.151;
		watchBox.isPDS = isPDS;
		watchBox.issued = DateTime.Parse((string)watch["properties"]["issue"]);
		watchBox.expires = DateTime.Parse((string)watch["properties"]["expire"]);

		var geometry = watch["geometry"]["coordinates"];
		SPCPolygon polygon = new();
		foreach (var coordinate in geometry[0][0])
			polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);

		watchBox.polygon = polygon;
		return watchBox;
	}

	/// <summary>
	/// Gets the likelihood of particular hazards from a specific severe thunderstorm/tornado watch.
	/// </summary>
	/// <remarks>
	/// Probabilities to percentages are as follow:
	/// Low: 5-20%
	/// | Moderate: 30-60%
	/// | High: >70%
	/// </remarks>
	/// <remarks>The most recent severe thunderstorm/tornado watches can be found at the Storm Prediction Centers website: <see href="https://www.spc.noaa.gov/"></see></remarks>
	/// <remarks><paramref name="watchNumber"/> is a parameter that specifies the watch number. Invalid watch numbers will still return a <see cref="WatchHazards"/> class, but all of the risks will be 0% with an empty <see cref="string"/>.</remarks>
	/// <returns>A <see cref="WatchHazards"/> class containing data about the possibility for tornadoes, severe wind, and severe hail.</returns>
	/// <exception cref="SPCWatchDoesntExistOrInvalidWatchNumberException"></exception>
	/// <param name="watchNumber">The number of the watch. (can be a tornado or severe thunderstorm watch)</param>
	/// <param name="year">The year of the watch.</param>
	public async Task<WatchHazards?> getWatchRisks(int watchNumber, int year)
	{
		string url = $"https://www.spc.noaa.gov/products/watch/{year}/ww{watchNumber:D4}.html"; // ensure the watch number is zero-padded to 4 digits

		string data = await Utils.downloadStringAsync(url);
		if (data == null || data == string.Empty)
			return null;
		HtmlDocument doc = new();
		doc.LoadHtml(data);

		HtmlNode table = doc.DocumentNode.SelectSingleNode("//table[@width='529' and @cellspacing='0' and @cellpadding='0' and @align='center']");
		if (table == null)
		{
			WatchHazards h = new()
			{
				message = "Newly issued. No details are available yet."
			};
			return h;
		}

		HtmlNodeCollection nodes = table.SelectNodes("//a[contains(@class,'wblack')]");
		if (nodes == null || nodes.Count < 6)
			throw new InvalidOperationException("The expected risk data could not be found in the HTML document.");

		WatchHazards watchHazards = new()
		{
			// check if the watch is a particularly dangerous situation (pds)
			isPDS = doc.Text.Contains("particularly dangerous situation", StringComparison.OrdinalIgnoreCase)
		};

		string[] split = table.InnerText.Split('\n', '|');

		// parse tornado risk
		string tornadoRisk = split[12].Replace("&nbsp;", " ");
		int tornadoRiskChance = int.Parse(nodes[0].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// parse significant tornado risk
		string sigTornadoRisk = split[13].Replace("&nbsp;", " ");
		int sigTornadoRiskChance = int.Parse(nodes[1].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// parse wind risk
		string windRisk = split[22].Replace("&nbsp;", " ");
		int windRiskChance = int.Parse(nodes[2].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// parse significant wind risk
		string sigWindRisk = split[23].Replace("&nbsp;", " ");
		int sigWindRiskChance = int.Parse(nodes[3].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// parse hail risk
		string hailRisk = split[32].Replace("&nbsp;", " ");
		int hailRiskChance = int.Parse(nodes[4].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// parse significant hail risk
		string sigHailRisk = split[33].Replace("&nbsp;", " ");
		int sigHailRiskChance = int.Parse(nodes[5].GetAttributeValue("title", string.Empty).Split('%')[0]);

		// populate the watch hazards object
		watchHazards.tornadoes = new(tornadoRiskChance, tornadoRisk);
		watchHazards.ef2PlusTornadoes = new(sigTornadoRiskChance, sigTornadoRisk);
		watchHazards.severeWind = new(windRiskChance, windRisk);
		watchHazards._65ktPlusWind = new(sigWindRiskChance, sigWindRisk);
		watchHazards.severeHail = new(hailRiskChance, hailRisk);
		watchHazards._2InchPlusHail = new(sigHailRiskChance, sigHailRisk);

		return watchHazards;
	}
}