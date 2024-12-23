using Azrellie.Misc.ExtendedTimer;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Dynamic;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

/*
						  Storm Prediction Center API for C#
								   Version 1.0.0
									Made by azzy
		This API was made for the need of me constantly having to access data
		from the Storm Prediction Center, most notably the convective outlooks,
		tornado watches, severe thunderstorm watches, and mesoscale discussions.

		The data that this API uses comes from the listed pages:
		1. https://www.spc.noaa.gov/gis/
		2. https://www.spc.noaa.gov/archive/
		3. https://www.weather.gov/documentation/services-web-api#/
		4. https://www.spc.noaa.gov/products/watch/ww0119.html (at the time of typing this)
		5. https://www.wpc.ncep.noaa.gov/kml/kmlproducts.php
		6. https://www.nhc.noaa.gov/gis/
		7. https://mrms.ncep.noaa.gov/data/RIDGEII/

		Said data is gathered up and processed to be used for whatever it is
		needed for without the hassle of retrieving the data, and processing it.

		This API can be used for many various things that use C# as its language.
		Whether thats software for Windows, games for Unity, or even for addons/mods
		that use C# to develop said addons/mods.

		This API was developed on the .NET 7 SDK. Backports are unlikely since I do not really
		have the time for that (but you are free to do it your self if you so wish).

		Porting to other languages like C++, python, javascript etc are possible, but the same statement
		above still applies.

		None of the classes within this API are nullable, which means you can safely read the
		fields and properties of the classes without having to do null checks, as null checks
		are handled internally and any null values will instead just use their default value.

		This API is still brand new and may have some bugs associated with it.

		Planned features for API may include:
		1. Ability to access historic data, whether thats outlooks or watches.
		2. Get a list of all active warnings from the National Weather Service, with the option to filter them by event name.
		3. Have a separate class for things solely related to the National Weather Service, such as observations and forecasts.
*/

// warnings begone
#pragma warning disable CS8600
#pragma warning disable CS8601
#pragma warning disable CS8602
#pragma warning disable CS8604
#pragma warning disable CS8767
#pragma warning disable CS1591

namespace Azrellie.Meteorology.SPC
{
	///////////// enums /////////////
	public enum CategoricalRiskType
	{
		GeneralThunderstorms = 2,
		Marginal = 3,
		Slight = 4,
		Enhanced = 5,
		Moderate = 6,
		High = 8
	}
	public enum TornadoRisk
	{
		_2Percent = 2,
		_5Percent = 5,
		_10Percent = 10,
		_15Percent = 15,
		_30Percent = 30,
		_45Percent = 45,
		_60Percent = 60
	}
	public enum WindHailRisk
	{
		_5Percent = 5,
		_15Percent = 15,
		_30Percent = 30,
		_45Percent = 45,
		_60Percent = 60
	}
	public enum RadarProduct
	{
		SR_BREF,
		SR_BVEL,
		BDHC,
		BDSA,
		BDOHA
	}
	public enum OutlookTime
	{
		Day1Time0100,
		Day1Time1200,
		Day1Time1300,
		Day1Time1630,
		Day1Time2000,
		Day2Time0600,
		Day2Time1730,
		Day3Time0730
	}
	///////////// enums /////////////

	///////////// exceptions /////////////
	public class InvalidSPCDayException : Exception
	{
		public InvalidSPCDayException() : base() { }
		public InvalidSPCDayException(string message) : base(message) { }
	}
	public class SPCOutlookDoesntExistException : Exception
	{
		public SPCOutlookDoesntExistException() : base() { }
		public SPCOutlookDoesntExistException(string message) : base(message) { }
	}
	public class SPCWatchDoesntExistOrInvalidWatchNumberException : Exception
	{
		public SPCWatchDoesntExistOrInvalidWatchNumberException() : base() { }
		public SPCWatchDoesntExistOrInvalidWatchNumberException(string message) : base(message) { }
	}
	///////////// exceptions /////////////

	///////////// list comparison stuff /////////////
	public class WatchComparer : IEqualityComparer<StormPredictionCenterWatch>
	{
		public bool Equals(StormPredictionCenterWatch watch1, StormPredictionCenterWatch watch2)
		{
			if (watch2 is null || watch1 is null) return false;
			return watch1.watchNumber == watch2.watchNumber; // if the watch number is the same, then everything else will be the same also
		}

		public int GetHashCode(StormPredictionCenterWatch obj)
		{
			return HashCode.Combine(obj.watchNumber);
		}
	}
	public class WatchBoxComparer : IEqualityComparer<StormPredictionCenterWatchBox>
	{
		public bool Equals(StormPredictionCenterWatchBox watch1, StormPredictionCenterWatchBox watch2)
		{
			if (watch2 is null || watch1 is null) return false;
			return watch1.watchNumber == watch2.watchNumber; // if the watch number is the same, then everything else will be the same also
		}

		public int GetHashCode(StormPredictionCenterWatchBox obj)
		{
			return HashCode.Combine(obj.watchNumber);
		}
	}
	///////////// list comparison stuff /////////////

	public class SPCPolygon
	{
		public List<double[]> coordinates = [];
	}
	public class SPCPoint
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

	///////////// geojson stuff /////////////
	public class Geometry
	{
		public string type = "";
		public List<dynamic> coordinates = [];
	}
	public class Feature
	{
		public string type = "Feature";
		public Geometry geometry = new();
		public ExpandoObject properties = new();
	}
	public class GeoJson
	{
		public string type = "FeatureCollection";
		public List<Feature> features = [];
	}
	///////////// geojson stuff /////////////

	///////////// SPC specific classes /////////////
	public class RiskArea
	{
		public object riskType = 0;
		public DateTime valid = DateTime.UnixEpoch;
		public DateTime expire = DateTime.UnixEpoch;
		public DateTime issue = DateTime.UnixEpoch;
		public string label = string.Empty;
		public string label2 = string.Empty;
		public Color stroke = Color.White;
		public Color fill = Color.White;
		public List<SPCPolygon> polygons = [];
		public override string ToString() => $"{label2} | {label} | Expires: {expire}, Issued: {issue}, Valid: {valid}";
	}
	///////////// SPC specific classes /////////////

	///////////// SPC mesoscale discussion classes /////////////
	public class StormPredictionCenterMesoscaleDiscussion
	{
		public string type = string.Empty;
		public string url = string.Empty;
		public string fullName = string.Empty;
		public int mesoscaleNumber = 0;
		public DateTimeOffset issued = DateTimeOffset.UnixEpoch;
		public string issuedString = string.Empty;
		public string areasAffected = string.Empty;
		public SPCPolygon polygon = new();
		public override string ToString() => $"{fullName} | Type: {type} | Issued: {issuedString} | More at: {url}";
	}
	///////////// SPC mesoscale discussion classes /////////////

	///////////// SPC watch specific classes /////////////
	public class WatchHazard(int chance, string hazard)
	{
		public int chance = chance;
		public string hazard = hazard;
	}
	public class WatchHazards
	{
		public bool isPDS = false;
		public WatchHazard tornadoes = new(0, string.Empty);
		public WatchHazard ef2PlusTornadoes = new(0, string.Empty);
		public WatchHazard severeWind = new(0, string.Empty);
		public WatchHazard _65ktPlusWind = new(0, string.Empty);
		public WatchHazard severeHail = new(0, string.Empty);
		public WatchHazard _2InchPlusHail = new(0, string.Empty);
		public override string ToString()
		{
			string pds = string.Empty;
			if (isPDS)
				pds = "PDS | ";
			return pds + $"Tornadoes: {tornadoes.chance}% | EF2+ Tornadoes: {ef2PlusTornadoes.chance}% | Severe Wind: {severeWind.chance}% | 65 kt+ Wind: {_65ktPlusWind.chance}% | Severe Hail: {severeHail.chance}% | 2\"+ Hail: {_2InchPlusHail.chance}%";
		}
	}
	public class CountyInfo
	{
		public string id = string.Empty;
		public string name = string.Empty;
		public string state = string.Empty;
		public List<string> forecastOffices = [];
		public string timeZone = string.Empty;
		public SPCPolygon geometry = new();
	}
	public class StormPredictionCenterWatch
	{
		public DateTime sent = DateTime.UnixEpoch;
		public DateTime effective = DateTime.UnixEpoch;
		public DateTime onset = DateTime.UnixEpoch;
		public DateTime expires = DateTime.UnixEpoch;
		public DateTime ends = DateTime.UnixEpoch;
		public int watchNumber = 0;
		public string description = string.Empty;
		public string sender = string.Empty;
		public string headline = string.Empty;
		public string watchType = string.Empty;
		public WatchHazards watchHazards = new();
		public List<CountyInfo> counties = [];
		public override string ToString() => $"{watchType} {watchNumber}";
	}
	///////////// SPC watch specific classes /////////////

	///////////// SPC watch specific classes /////////////
	public class StormPredictionCenterWarning
	{
		public DateTime sent = DateTime.UnixEpoch;
		public DateTime effective = DateTime.UnixEpoch;
		public DateTime onset = DateTime.UnixEpoch;
		public DateTime expires = DateTime.UnixEpoch;
		public DateTime ends = DateTime.UnixEpoch;
		public int watchNumber = 0;
		public string warningName = string.Empty;
		public string description = string.Empty;
		public string instruction = string.Empty;
		public string sender = string.Empty;
		public string headline = string.Empty;
		public string nwsHeadline = string.Empty;
		public string windThreat = string.Empty;
		public string maxWindGust = string.Empty;
		public string hailThreat = string.Empty;
		public string maxHailSize = string.Empty;
		public string tornadoDetection = string.Empty;
		public string thunderstormDamageThreat = string.Empty;
		public string flashFloodDamageThreat = string.Empty;
		public string flashFloodDetection = string.Empty;
		public string cmamText = string.Empty;
		public string cmamLongText = string.Empty;
		public SPCPolygon polygon = new();
	}
	///////////// SPC watch specific classes /////////////

	///////////// SPC watch box classes /////////////
	public class StormPredictionCenterWatchBox
	{
		public string watchType = string.Empty;
		public string watchName = string.Empty;
		public int watchNumber = -1;
		public double maxHailSizeInches = -1;
		public double maxWindGustMph = -1;
		public bool isPDS = false;
		public DateTime issued = DateTime.MinValue;
		public DateTime expires = DateTime.MinValue;
		public SPCPolygon polygon = new();
		public override string ToString()
		{
			string pdsString = string.Empty;
			if (isPDS)
				pdsString = "PDS ";
			return $"{pdsString}{watchType} {watchNumber} | Max Hail Size: {Math.Floor(maxHailSizeInches * 100) / 100} in | Max Wind Gust: {Math.Floor(maxWindGustMph * 100) / 100} mph";
		}
	}
	///////////// SPC watch box classes /////////////

	///////////// NWS radar station classes /////////////
	public class Elevation
	{
		public string unit = string.Empty;
		public double elevation = 0;
	}
	public class RadarStation
	{
		public SPCPoint location = new();
		public string id = string.Empty;
		public string name = string.Empty;
		public string stationType = string.Empty;
		public string timeZone = string.Empty;
		public string mode = string.Empty;
		public Elevation elevation = new();
	}
	///////////// NWS radar station classes /////////////

	///////////// NHC classes /////////////
	public class WindRadii
	{
		public int windSpeedKts { get; set; } = 0;
		public List<double[]> coordinates { get; set; } = [];
	}
	public class ForecastCone
	{
		public string timezone { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public DateTime advisoryDate { get; set; } = DateTime.UnixEpoch;
		public string basin { get; set; } = string.Empty;
		public string advisoryNumber { get; set; } = string.Empty;
		public int stormNumber { get; set; } = 0;
		public string stormName { get; set; } = string.Empty;
		public List<double[]> coordinates { get; set; } = [];
	}
	public class ForecastPoint
	{
		public double latitude = 0;
		public double longitude = 0;
		public string stormName = string.Empty;
		public int stormNumber = 0;
		public string basin = string.Empty;
		public string type = string.Empty;
		public int intensityKts = 0;
		public int intensityMph = 0;
		public int intensityKmh = 0;
		public int windGustsKts = 0;
		public int windGustsMph = 0;
		public int minSeaLevelPressure = 0;
		public string forecastHour = string.Empty;
		public DateTime date = DateTime.UnixEpoch;
	}
	public class StormObject
	{
		public string type { get; set; } = string.Empty;
		public string name { get; set; } = string.Empty;
		public string wallet { get; set; } = string.Empty;
		public double centerLat { get; set; }
		public double centerLng { get; set; }
		public DateTime dateTime { get; set; }
		public string movement { get; set; } = string.Empty;
		public int minimumPressureMbar { get; set; }
		public int maxSustainedWindsMph { get; set; }
		public string headline { get; set; } = string.Empty;
		public List<ForecastPoint> forecastPoints { get; set; } = [];
		public List<ForecastPoint> pastTrack { get; set; } = [];
		public ForecastCone forecastCone { get; set; } = new();
		public List<WindRadii> WindRadii { get; set; } = [];
	}
	public class DisturbanceObject
	{
		public SPCPolygon polygon { get; set; } = new();
		public SPCPoint point { get; set; } = new();
		public byte disturbanceIndex { get; set; } = 0;
		public string day2Percentage { get; set; } = string.Empty;
		public string day2Category { get; set; } = string.Empty;
		public string day7Percentage { get; set; } = string.Empty;
		public string day7Category { get; set; } = string.Empty;
		public string discussion { get; set; } = string.Empty;
		public override string ToString() => $"Disturbance {disturbanceIndex} - {day2Percentage} chance of cyclone formation in 48 hours - {day7Percentage} chance of cyclone formation in 7 days";
	}
	///////////// NHC classes /////////////

	public class StormPredictionCenter
	{
		/// <summary>
		/// The version of this API as a string. Useful if you need to check the version of the API through code.
		/// </summary>
		public readonly string versionString = "1.0.0";

		/// <summary>
		/// The version of this API as a double. Useful if you need to check the version of the API through code.
		/// </summary>
		public readonly double versionNumber = 100;

		public Outlooks outlooks;
		public Watches watches;
		public Warnings warnings;
		public Archive archive;
		public NHC nhc;
		public Radar radar;
		public Events events;

		/// <summary>
		/// Retrieve certain outlooks from the SPC.
		/// </summary>
		public class Outlooks
		{
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
			public RiskArea[] getCategoricalOutlook(int year, int month, int day, int relativeDay, OutlookTime time)
			{
				// day 0 wind outlook doesnt exist, it cant hurt you
				// day 0 wind outlook:
				if (relativeDay < 1 || relativeDay > 3)
					throw new InvalidSPCDayException($"'{relativeDay}' is not a valid SPC outlook day.");

				string strDay = day.ToString();
				if (day < 10)
					strDay = "0" + strDay;
				string strMonth = month.ToString();
				if (month < 10)
					strMonth = "0" + month;
				string url = $"https://www.spc.noaa.gov/products/outlook/archive/{year}/day{relativeDay}otlk_{year}{strMonth}{strDay}_{time.ToString().Replace("Time", string.Empty)}_cat.nolyr.geojson";

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
								foreach (var polygonMain in polygons)
									foreach (var polygon in polygonMain)
									{
										SPCPolygon polygonClass = new();
										foreach (var coordinate in polygon)
											polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
										riskArea.polygons.Add(polygonClass);
									}
							else if (type == "Polygon")
								foreach (var polygon in polygons)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}

						spcObject.Add(riskArea);
					}
				return [.. spcObject];
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
			public RiskArea[] getCategoricalOutlookDay4Plus(int year, int month, int day)
			{
				string strDay = day.ToString();
				if (day < 10)
					strDay = "0" + strDay;
				string strMonth = month.ToString();
				if (month < 10)
					strMonth = "0" + month;

				string stringData = Utils.downloadString($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_cat.nolyr.geojson");
				string stringData2 = Utils.downloadString($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_prob.nolyr.geojson");
				string stringData3 = Utils.downloadString($"https://www.spc.noaa.gov/products/outlook/archive/{year}/day3otlk_{year}{strMonth}{strDay}_0730_sigprob.nolyr.geojson");

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
								foreach (var polygonMain in polygons)
									foreach (var polygon in polygonMain)
									{
										SPCPolygon polygonClass = new();
										foreach (var coordinate in polygon)
											polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
										riskArea.polygons.Add(polygonClass);
									}
							else if (type == "Polygon")
								foreach (var polygon in polygons)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
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
								foreach (var polygonMain in polygons)
									foreach (var polygon in polygonMain)
									{
										SPCPolygon polygonClass = new();
										foreach (var coordinate in polygon)
											polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
										riskArea.polygons.Add(polygonClass);
									}
							else if (type == "Polygon")
								foreach (var polygon in polygons)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
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
								foreach (var polygonMain in polygons)
									foreach (var polygon in polygonMain)
									{
										SPCPolygon polygonClass = new();
										foreach (var coordinate in polygon)
											polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
										riskArea.polygons.Add(polygonClass);
									}
							else if (type == "Polygon")
								foreach (var polygon in polygons)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}

						spcObject.Add(riskArea);
					}

				return [.. spcObject];
			}

			/// <summary>
			/// Gets the latest categorical outlook from the Storm Prediction Center.
			/// </summary>
			/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
			/// <exception cref="InvalidSPCDayException"/>
			/// <exception cref="SPCOutlookDoesntExistException"/>
			public RiskArea[] getLatestCategoricalOutlook(int day = 1)
			{
				if (day < 1 || day > 3)
					throw new InvalidSPCDayException($"Day {day} is not a valid SPC outlook day.");

				string stringData = Utils.downloadString($"https://www.spc.noaa.gov/products/outlook/day{day}otlk_cat.nolyr.geojson");

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
								foreach (var polygonMain in polygons)
									foreach (var polygon in polygonMain)
									{
										SPCPolygon polygonClass = new();
										foreach (var coordinate in polygon)
											polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
										riskArea.polygons.Add(polygonClass);
									}
							else if (type == "Polygon")
								foreach (var polygon in polygons)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}

						spcObject.Add(riskArea);
					}
				return [.. spcObject];
			}

			/// <summary>
			/// Similar to <see cref="getLatestCategoricalOutlook()"/>, but is specifically for getting the latest data from categorical outlooks days 4 to 8.
			/// <para><paramref name="day"/> parameter is clamped between 4-8, and defaults to 4 if not specified.</para>
			/// </summary>
			/// <param name="day">The day of the categorical outlook.</param>
			/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
			/// <exception cref="InvalidSPCDayException"/>
			/// <exception cref="SPCOutlookDoesntExistException"/>
			public RiskArea[] getLatestCategoricalOutlookDay4Plus(int day = 4)
			{
				// day 0 categorical outlook doesnt exist, it cant hurt you
				// day 0 categorical risk:
				if (day < 4 || day > 8)
					throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

				string stringData = Utils.downloadString($"https://www.spc.noaa.gov/products/exper/day4-8/day{day}prob.nolyr.geojson");
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
							foreach (var polygonMain in polygons)
								foreach (var polygon in polygonMain)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}
						else if (type == "Polygon")
							foreach (var polygon in polygons)
							{
								SPCPolygon polygonClass = new();
								foreach (var coordinate in polygon)
									polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
								riskArea.polygons.Add(polygonClass);
							}

					spcObject.Add(riskArea);
				}
				return [.. spcObject];
			}

			/// <summary>
			/// Gets the latest tornado outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the tornado outlook that should be retrieved.
			/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
			/// </summary>
			/// <param name="day">The day of the tornado outlook.</param>
			/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
			/// <exception cref="InvalidSPCDayException"/>
			/// <exception cref="SPCOutlookDoesntExistException"/>
			public RiskArea[] getLatestTornadoOutlook(int day = 1)
			{
				// day 0 tornado outlook doesnt exist, it cant hurt you
				// day 0 tornado outlook:
				if (day < 1 || day > 2)
					throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

				DateTime now = DateTime.Now;
				string url = $"https://www.spc.noaa.gov/products/outlook/archive/{now.Year}/day{day}otlk_{now.Year}{now:MM}{now:dd}_TIME_torn.nolyr.geojson";
				// since theres no way to know the time of the most recent outlook
				// we will have to iterate through the possible times and see which url returns valid data
				(bool exists, string time) = Utils.doesOutlookTimeExist(url, day);
				if (exists)
					url = url.Replace("TIME", time); // it exists, go time
				else
					throw new SPCOutlookDoesntExistException($"The SPC tornado outlook for {now:MMMM dd} does not exist."); // it doesnt exist 🗿

				string stringData = Utils.downloadString(url);
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

					// iterate through the risk areas polygons/coordinates
					var polygons = feature["geometry"]["coordinates"];
					string type = (string)feature["geometry"]["type"];
					if (polygons != null)
						if (type == "MultiPolygon")
							foreach (var polygonMain in polygons)
								foreach (var polygon in polygonMain)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}
						else if (type == "Polygon")
							foreach (var polygon in polygons)
							{
								SPCPolygon polygonClass = new();
								foreach (var coordinate in polygon)
									polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
								riskArea.polygons.Add(polygonClass);
							}

					spcObject.Add(riskArea);
				}

				return [.. spcObject];
			}

			/// <summary>
			/// Gets the latest wind outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the wind outlook that should be retrieved.
			/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
			/// </summary>
			/// <param name="day">The day of the wind outlook.</param>
			/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
			/// <exception cref="InvalidSPCDayException"/>
			/// <exception cref="SPCOutlookDoesntExistException"/>
			public RiskArea[] getLatestWindOutlook(int day = 1)
			{
				// day 0 wind outlook doesnt exist, it cant hurt you
				// day 0 wind outlook:
				if (day < 1 || day > 2)
					throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

				DateTime now = DateTime.Now;
				string url = $"https://www.spc.noaa.gov/products/outlook/archive/{now.Year}/day{day}otlk_{now.Year}{now:MM}{now:dd}_TIME_wind.nolyr.geojson";

				// since theres no way to know the time of the most recent outlook
				// we will have to iterate through the possible times and see which url returns valid data
				(bool exists, string time) = Utils.doesOutlookTimeExist(url, day);
				if (exists)
					url = url.Replace("TIME", time); // it exists, go time
				else
					throw new SPCOutlookDoesntExistException($"The SPC wind outlook for {now:MMMM dd} does not exist."); // it doesnt exist 🗿

				string stringData = Utils.downloadString(url);
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

					// iterate through the risk areas polygons/coordinates
					var polygons = feature["geometry"]["coordinates"];
					string type = (string)feature["geometry"]["type"];
					if (polygons != null)
						if (type == "MultiPolygon")
							foreach (var polygonMain in polygons)
								foreach (var polygon in polygonMain)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}
						else if (type == "Polygon")
							foreach (var polygon in polygons)
							{
								SPCPolygon polygonClass = new();
								foreach (var coordinate in polygon)
									polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
								riskArea.polygons.Add(polygonClass);
							}

					spcObject.Add(riskArea);
				}
				return [.. spcObject];
			}

			/// <summary>
			/// Gets the latest hail outlook from the Storm Prediction Center, with a <see cref="int">day</see> parameter to specify the day of the hail outlook that should be retrieved.
			/// <para><paramref name="day"/> parameter is clamped between 1-2, and defaults to 1 if not specified.</para>
			/// </summary>
			/// <param name="day">The day of the hail outlook.</param>
			/// <returns>An array of <see cref="RiskArea"/> objects which contains the processed data in a more easy to use format.</returns>
			/// <exception cref="InvalidSPCDayException"/>
			/// <exception cref="SPCOutlookDoesntExistException"/>
			public RiskArea[] getLatestHailOutlook(int day = 1)
			{
				// day 0 hail outlook doesnt exist, it cant hurt you
				// day 0 hail outlook:
				if (day < 1 || day > 2)
					throw new InvalidSPCDayException($"'{day}' is not a valid SPC outlook day.");

				DateTime now = DateTime.Now;
				string url = $"https://www.spc.noaa.gov/products/outlook/archive/{now.Year}/day{day}otlk_{now.Year}{now:MM}{now:dd}_TIME_hail.nolyr.geojson";

				// since theres no way to know the time of the most recent outlook
				// we will have to iterate through the possible times and see which url returns valid data
				(bool exists, string time) = Utils.doesOutlookTimeExist(url, day);
				if (exists)
					url = url.Replace("TIME", time); // it exists, go time
				else
					throw new SPCOutlookDoesntExistException($"The SPC hail outlook for {now:MMMM dd} does not exist."); // it doesnt exist 🗿

				string stringData = Utils.downloadString(url);
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

					// iterate through the risk areas polygons/coordinates
					var polygons = feature["geometry"]["coordinates"];
					string type = (string)feature["geometry"]["type"];
					if (polygons != null)
						if (type == "MultiPolygon")
							foreach (var polygonMain in polygons)
								foreach (var polygon in polygonMain)
								{
									SPCPolygon polygonClass = new();
									foreach (var coordinate in polygon)
										polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
									riskArea.polygons.Add(polygonClass);
								}
						else if (type == "Polygon")
							foreach (var polygon in polygons)
							{
								SPCPolygon polygonClass = new();
								foreach (var coordinate in polygon)
									polygonClass.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
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
			public StormPredictionCenterMesoscaleDiscussion[] getLatestMesoscaleDiscussion()
			{
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

				string temp = Path.GetTempPath();
				if (!Directory.Exists(temp + "\\spc temp"))
					Directory.CreateDirectory(temp + "\\spc temp");
				string mdKmz = temp + "\\spc temp\\md.kmz";
				string activeMds = temp + "\\spc temp\\active md";
				if (!Directory.Exists(activeMds))
					Directory.CreateDirectory(activeMds);

				// delete existing kmz/kml files, since these can screw with extraction of new ones
				if (File.Exists(mdKmz))
					File.Delete(mdKmz);
				if (File.Exists(temp + "\\ActiveMD.kml"))
					File.Delete(temp + "\\ActiveMD.kml");
				foreach (string file in Directory.GetFiles(activeMds))
					File.Delete(file);

				Utils.downloadFile("https://www.spc.noaa.gov/products/md/ActiveMD.kmz", mdKmz);
				ZipFile.ExtractToDirectory(mdKmz, temp);

				// extract active mesoscale discussion kmz files and store it in active md folder
				Parser activeKmzParser = new();
				activeKmzParser.ParseString(File.ReadAllText(temp + "\\ActiveMD.kml"), false);
				if (activeKmzParser.Root is Kml activeMdKmz)
					foreach (var activeMd in activeMdKmz.Flatten().OfType<Folder>().SelectMany(folder => folder.Flatten().OfType<Link>().Select(link => link.Href)))
					{
						string fileName = Path.GetFileName(activeMd.KmzUrl().AbsoluteUri);
						Utils.downloadFile(activeMd.KmzUrl().AbsoluteUri, activeMds + "\\" + fileName);
					}

				// extract those md kmz files to kml, then delete the kmz files
				foreach (string kmz in Directory.GetFiles(activeMds))
				{
					ZipFile.ExtractToDirectory(kmz, activeMds);
					File.Delete(kmz);
				}

				// begin reading the md kml files
				List<StormPredictionCenterMesoscaleDiscussion> stormPredictionCenterMesoscaleDiscussions = [];
				foreach (string mesoscaleDiscussion in Directory.GetFiles(activeMds))
				{
					StormPredictionCenterMesoscaleDiscussion stormPredictionCenterMesoscaleDiscussion = new();
					Parser parser = new();
					parser.ParseString(File.ReadAllText(mesoscaleDiscussion), false);
					if (parser.Root is Kml mdKml)
						foreach (var feature in mdKml.Flatten().OfType<Placemark>())
						{
							SPCPolygon polygon = new();
							Polygon geometry = feature.Geometry as Polygon;
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

				return [.. stormPredictionCenterMesoscaleDiscussions];
			}
		}

		/// <summary>
		/// Retrieve certain watches from the SPC.
		/// </summary>
		public class Watches
		{
			/// <summary>
			/// Gets currently active severe thunderstorm watches from the National Weather Service.
			/// </summary>
			/// <returns>A <see cref="StormPredictionCenterWatch"/> class object which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWatch[] getActiveSevereThunderstormWatches()
			{
				List<StormPredictionCenterWatch> stormPredictionCenterWatches = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is JObject jsonData)
					foreach (var alert in jsonData["features"])
					{
						string eventName = (string)alert["properties"]["event"];
						if (eventName == "Severe Thunderstorm Watch")
						{
							string description = (string)alert["properties"]["description"];
							int watchNumber = Utils.getSevereThunderstormWatchNumber(description);
							StormPredictionCenterWatch stormPredictionCenterWatch = new();
							foreach (string? countyAffected in alert["properties"]["affectedZones"])
							{
								if (countyAffected == null) continue;
								JObject? county = JsonConvert.DeserializeObject(Utils.downloadString(countyAffected)) as JObject;
								string geometryType = (string)county["geometry"]["type"];
								// iterate through the polygons coordinates of the affected county
								var polygonCoordinates = county["geometry"]["coordinates"];
								CountyInfo countyInfo = new();
								SPCPolygon polygon = new();
								if (geometryType == "SPCPolygon")
									foreach (var coordinate in polygonCoordinates[0])
										polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
								else if (geometryType == "MultiPolygon")
									foreach (var multiPolygon in polygonCoordinates)
										foreach (var subPolygon in multiPolygon)
											foreach (var coordinate in subPolygon)
												polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

								countyInfo.id = (string)county["properties"]["id"];
								countyInfo.name = (string)county["properties"]["name"];
								countyInfo.state = (string)county["properties"]["state"];
								foreach (string forecastOffice in county["properties"]["forecastOffices"])
									countyInfo.forecastOffices.Add(forecastOffice);
								countyInfo.timeZone = (string)county["properties"]["timeZone"][0];
								countyInfo.geometry = polygon;
								stormPredictionCenterWatch.sent = DateTime.Parse((string)alert["properties"]["sent"]);
								stormPredictionCenterWatch.effective = DateTime.Parse((string)alert["properties"]["effective"]);
								stormPredictionCenterWatch.onset = DateTime.Parse((string)alert["properties"]["onset"]);
								stormPredictionCenterWatch.expires = DateTime.Parse((string)alert["properties"]["expires"]);
								stormPredictionCenterWatch.ends = DateTime.Parse((string)alert["properties"]["ends"]);
								stormPredictionCenterWatch.sender = (string)alert["properties"]["senderName"];
								stormPredictionCenterWatch.headline = (string)alert["properties"]["headline"];
								stormPredictionCenterWatch.description = description;
								stormPredictionCenterWatch.watchType = (string)alert["properties"]["event"];
								stormPredictionCenterWatch.watchNumber = watchNumber;
								stormPredictionCenterWatch.watchHazards = getWatchRisks(watchNumber, DateTime.UtcNow.Year);
								stormPredictionCenterWatch.counties.Add(countyInfo);
							}
							stormPredictionCenterWatches.Add(stormPredictionCenterWatch);
						}
					}
				var dupeRemovedList = stormPredictionCenterWatches.Distinct(new WatchComparer()).ToList();
				stormPredictionCenterWatches = dupeRemovedList;
				return [.. stormPredictionCenterWatches];
			}

			/// <summary>
			/// Gets currently active tornado watches from the National Weather Service.
			/// </summary>
			/// <returns>A <see cref="StormPredictionCenterWatch"/> class object which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWatch[] getActiveTornadoWatches()
			{
				List<StormPredictionCenterWatch> stormPredictionCenterWatches = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is JObject jsonData)
					foreach (var alert in jsonData["features"])
					{
						string eventName = (string)alert["properties"]["event"];
						if (eventName == "Tornado Watch")
						{
							string description = (string)alert["properties"]["description"];
							int watchNumber = Utils.getTornadoWatchNumber(description);
							StormPredictionCenterWatch stormPredictionCenterWatch = new();
							foreach (string? countyAffected in alert["properties"]["affectedZones"])
							{
								if (countyAffected == null) continue;
								JObject? county = JsonConvert.DeserializeObject(Utils.downloadString(countyAffected)) as JObject;
								string geometryType = (string)county["geometry"]["type"];
								// iterate through the polygons coordinates of the affected county
								var polygonCoordinates = county["geometry"]["coordinates"];
								CountyInfo countyInfo = new();
								SPCPolygon polygon = new();
								if (geometryType == "Polygon")
									foreach (var coordinate in polygonCoordinates[0])
										polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);
								else if (geometryType == "MultiPolygon")
									foreach (var multiPolygon in polygonCoordinates)
										foreach (var subPolygon in multiPolygon)
											foreach (var coordinate in subPolygon)
												polygon.coordinates.Add([(double)coordinate[0], (double)coordinate[1]]);

								countyInfo.id = (string)county["properties"]["id"];
								countyInfo.name = (string)county["properties"]["name"];
								countyInfo.state = (string)county["properties"]["state"];
								foreach (string forecastOffice in county["properties"]["forecastOffices"])
									countyInfo.forecastOffices.Add(forecastOffice);
								countyInfo.timeZone = (string)county["properties"]["timeZone"][0];
								countyInfo.geometry = polygon;
								stormPredictionCenterWatch.sent = DateTime.Parse((string)alert["properties"]["sent"]);
								stormPredictionCenterWatch.effective = DateTime.Parse((string)alert["properties"]["effective"]);
								stormPredictionCenterWatch.onset = DateTime.Parse((string)alert["properties"]["onset"]);
								stormPredictionCenterWatch.expires = DateTime.Parse((string)alert["properties"]["expires"]);
								stormPredictionCenterWatch.ends = DateTime.Parse((string)alert["properties"]["ends"]);
								stormPredictionCenterWatch.sender = (string)alert["properties"]["senderName"];
								stormPredictionCenterWatch.headline = (string)alert["properties"]["headline"];
								stormPredictionCenterWatch.description = description;
								stormPredictionCenterWatch.watchType = (string)alert["properties"]["event"];
								stormPredictionCenterWatch.watchNumber = watchNumber;
								stormPredictionCenterWatch.watchHazards = getWatchRisks(watchNumber, DateTime.UtcNow.Year);
								stormPredictionCenterWatch.counties.Add(countyInfo);
							}
							stormPredictionCenterWatches.Add(stormPredictionCenterWatch);
						}
					}
				var dupeRemovedList = stormPredictionCenterWatches.Distinct(new WatchComparer()).ToList();
				stormPredictionCenterWatches = dupeRemovedList;
				return [.. stormPredictionCenterWatches];
			}

			/// <summary>
			/// Gets watch boxes for currently active severe thunderstorm/tornado watches from the Storm Prediction Center.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWatchBox"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWatchBox[] getActiveWatchBoxes()
			{
				string temp = Path.GetTempPath();
				if (!Directory.Exists(temp + "\\spc temp"))
					Directory.CreateDirectory(temp + "\\spc temp");
				string wwKmz = temp + "\\spc temp\\ww.kmz";
				string activeWws = temp + "\\spc temp\\active wws";
				if (!Directory.Exists(activeWws))
					Directory.CreateDirectory(activeWws);

				// delete existing kmz/kml files, since these can screw with extraction of new ones
				if (File.Exists(wwKmz))
					File.Delete(wwKmz);
				if (File.Exists(temp + "\\ActiveWW.kml"))
					File.Delete(temp + "\\ActiveWW.kml");
				foreach (string file in Directory.GetFiles(activeWws))
					File.Delete(file);
				Utils.downloadFile("https://www.spc.noaa.gov/products/watch/ActiveWW.kmz", wwKmz);

				ZipFile.ExtractToDirectory(wwKmz, temp);

				// extract active mesoscale discussion kmz files and store it in active_md folder
				Parser activeKmzParser = new();
				activeKmzParser.ParseString(File.ReadAllText(temp + "\\ActiveWW.kml"), false);
				if (activeKmzParser.Root is Kml activeMdKmz)
					foreach (var activeMd in activeMdKmz.Flatten().OfType<Folder>().SelectMany(folder => folder.Flatten().OfType<Link>().Select(link => link.Href)))
					{
						string fileName = Path.GetFileName(activeMd.KmzUrl().AbsoluteUri);
						Utils.downloadFile(activeMd.KmzUrl().AbsoluteUri, activeWws + "\\" + fileName);
					}

				// extract those md kmz files to kml, then delete the kmz files
				foreach (string kmz in Directory.GetFiles(activeWws))
				{
					ZipFile.ExtractToDirectory(kmz, activeWws);
					File.Delete(kmz);
				}

				// begin reading the md kml files
				List<StormPredictionCenterWatchBox> stormPredictionCenterWatchBoxes = [];
				foreach (string watchBox in Directory.GetFiles(activeWws))
				{
					StormPredictionCenterWatchBox stormPredictionCenterWatchBox = new();
					Parser parser = new();
					parser.ParseString(File.ReadAllText(watchBox), false);
					if (parser.Root is Kml mdKml)
						foreach (var feature in mdKml.Flatten().OfType<Placemark>())
						{
							SPCPolygon polygon = new();
							if (feature.Geometry is Polygon geometry)
								foreach (Vector vector in geometry.OuterBoundary.LinearRing.Coordinates)
									polygon.coordinates.Add([vector.Latitude, vector.Longitude]);
							stormPredictionCenterWatchBox.polygon = polygon;
							stormPredictionCenterWatchBox.watchName = feature.Name;
							stormPredictionCenterWatchBox.watchNumber = int.Parse(feature.Name[3..7]);
							stormPredictionCenterWatchBox.isPDS = feature.Name.Contains("pds", StringComparison.CurrentCultureIgnoreCase) || feature.Name.Contains("particularly dangerous situation", StringComparison.CurrentCultureIgnoreCase);
							stormPredictionCenterWatchBox.watchType = feature.StyleUrl.ToString().Trim('#');
						}
					stormPredictionCenterWatchBoxes.Add(stormPredictionCenterWatchBox);
				}

				return [.. stormPredictionCenterWatchBoxes];
			}

			/// <summary>
			/// Gets archived tornado and severe thunderstorm watches from the National Weather Service and Iowa Environmental Mesonet.
			/// </summary>
			/// <returns>An array of <see cref="StormPredictionCenterWatch"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWatchBox[] getArchivedWatches(int year, int month, int day, string time = "")
			{
				List<StormPredictionCenterWatchBox> stormPredictionCenterWatchBoxes = [];
				string strMonth = month.ToString();
				if (month < 10)
					strMonth = "0" + strMonth;
				string strDay = day.ToString();
				if (day < 10)
					strDay = "0" + strDay;
				if (time != string.Empty)
				{
					if (JsonConvert.DeserializeObject(Utils.downloadString($"https://mesonet.agron.iastate.edu/json/spcwatch.py?ts={year}{strMonth}{strDay}{time}&fmt=geojson")) is JObject jsonData)
						foreach (var watch in jsonData["features"])
						{
							StormPredictionCenterWatchBox watchBox = new();
							string watchType = (string)watch["properties"]["type"];
							int watchNumber = (int)watch["properties"]["number"];
							bool isPDS = (bool)watch["properties"]["is_pds"];
							string pdsTag = string.Empty;
							if (isPDS)
								pdsTag = "PDS ";
							if (watchType == "TOR")
								watchBox.watchName = pdsTag + "Tornado Watch " + watchNumber;
							else if (watchType == "SVR")
								watchBox.watchName = pdsTag + "Severe Thunderstorm Watch " + watchNumber;
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

							stormPredictionCenterWatchBoxes.Add(watchBox);
						}
				}
				else
				{
					for (int i = 0; i < 24; i++) // iterate hour by hour from the start of the day to the end of the day
					{
						if (JsonConvert.DeserializeObject(Utils.downloadString($"https://mesonet.agron.iastate.edu/json/spcwatch.py?ts={year}{strMonth}{strDay}{i * 100:0000}&fmt=geojson")) is JObject jsonData)
							foreach (var watch in jsonData["features"])
							{
								StormPredictionCenterWatchBox watchBox = new();
								string watchType = (string)watch["properties"]["type"];
								int watchNumber = (int)watch["properties"]["number"];
								bool isPDS = (bool)watch["properties"]["is_pds"];
								string pdsTag = string.Empty;
								if (isPDS)
									pdsTag = "PDS ";
								if (watchType == "TOR")
									watchBox.watchName = pdsTag + "Tornado Watch " + watchNumber;
								else if (watchType == "SVR")
									watchBox.watchName = pdsTag + "Severe Thunderstorm Watch " + watchNumber;
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

								stormPredictionCenterWatchBoxes.Add(watchBox);
							}
					}
				}
				var dupeRemovedList = stormPredictionCenterWatchBoxes.Distinct(new WatchBoxComparer()).ToList();
				stormPredictionCenterWatchBoxes = dupeRemovedList;
				return [.. stormPredictionCenterWatchBoxes];
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
			public WatchHazards getWatchRisks(int watchNumber, int year)
			{
				string watchNumberStr = watchNumber.ToString();
				if (watchNumber < 10)
					watchNumberStr = "000" + watchNumber;
				else if (watchNumber >= 10 && watchNumber < 100)
					watchNumberStr = "00" + watchNumber;
				else if (watchNumber >= 100 && watchNumber < 1000)
					watchNumberStr = "0" + watchNumber;

				string url = $"https://www.spc.noaa.gov/products/watch/{year}/ww{watchNumberStr}.html";
				HtmlWeb web = new();
				HtmlDocument doc = web.Load(url);
				HtmlNode table = doc.DocumentNode.SelectSingleNode("//table[@width='529' and @cellspacing='0' and @cellpadding='0' and @align='center']") ?? throw new SPCWatchDoesntExistOrInvalidWatchNumberException($"{watchNumber} is an invalid watch number because severe thunderstorm watch/tornado watch {watchNumber} do not exist.");
				HtmlNodeCollection nodes = table.SelectNodes("//a[contains(@class,'wblack')]");
				WatchHazards watchHazards = new();

				bool isPDS = doc.Text.Contains("particularly dangerous situation", StringComparison.OrdinalIgnoreCase);
				string[] split = table.InnerText.Split('\n', '|');

				// tornado risk
				string tornadoRisk = split[12].Replace("&nbsp;", " ");
				int tornadoRiskChance = int.Parse(nodes[0].GetAttributeValue("title", string.Empty).Split('%')[0]);

				// sig tornado risk
				string sigTornadoRisk = split[13].Replace("&nbsp;", " ");
				int sigTornadoRiskChance = int.Parse(nodes[1].GetAttributeValue("title", string.Empty).Split('%')[0]);

				// wind risk
				string windRisk = split[22].Replace("&nbsp;", " ");
				int windRiskChance = int.Parse(nodes[2].GetAttributeValue("title", string.Empty).Split('%')[0]);

				// sig wind risk
				string sigWindRisk = split[23].Replace("&nbsp;", " ");
				int sigWindRiskChance = int.Parse(nodes[3].GetAttributeValue("title", string.Empty).Split('%')[0]);

				// hail risk
				string hailRisk = split[32].Replace("&nbsp;", " ");
				int hailRiskChance = int.Parse(nodes[4].GetAttributeValue("title", string.Empty).Split('%')[0]);

				// sig hail risk
				string sigHailRisk = split[33].Replace("&nbsp;", " ");
				int sigHailRiskChance = int.Parse(nodes[5].GetAttributeValue("title", string.Empty).Split('%')[0]);

				watchHazards.tornadoes = new(tornadoRiskChance, tornadoRisk);
				watchHazards.ef2PlusTornadoes = new(sigTornadoRiskChance, sigTornadoRisk);
				watchHazards.severeWind = new(windRiskChance, windRisk);
				watchHazards._65ktPlusWind = new(sigWindRiskChance, sigWindRisk);
				watchHazards.severeHail = new(hailRiskChance, hailRisk);
				watchHazards._2InchPlusHail = new(sigHailRiskChance, sigHailRisk);
				watchHazards.isPDS = isPDS;

				return watchHazards;
			}
		}

		/// <summary>
		/// Retrieve warning data from the NWS.
		/// </summary>
		public class Warnings
		{
			/// <summary>
			/// Gets currently active convective warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestConvectiveWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "Tornado Warning" ||
						alert == "Severe Thunderstorm Warning" ||
						alert == "Special Weather Statement" ||
						alert == "Tornado Watch" ||
						alert == "Severe Thunderstorm Watch" ||
						alert == "Severe Weather Statement";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active hydrologic warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestHydrologicWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "Flash Flood Warning" ||
						alert == "Flood Warning" ||
						alert == "Flash Flood Watch" ||
						alert == "Flood Watch" ||
						alert == "Flood Advisory" ||
						alert == "Flood Statement" ||
						alert == "Hydrologic Statement" ||
						alert == "Hydrologic Outlook" ||
						alert == "Flash Flood Statement";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active non convective warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestNonConvectiveWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "High Wind Warning" ||
						alert == "High Wind Watch" ||
						alert == "Wind Advisory" ||
						alert == "Excessive Heat Warning" ||
						alert == "Heat Advisory" ||
						alert == "Hard Freeze Warning" ||
						alert == "Hard Freeze Watch" ||
						alert == "Freeze Warning" ||
						alert == "Freeze Watch" ||
						alert == "Dense Fog Advisory" ||
						alert == "Dense Smoke Advisory" ||
						alert == "Dust Storm Warning" ||
						alert == "Blowing Dust Advisory" ||
						alert == "Air Stagnation Advisory" ||
						alert == "Air Quality Alert";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active marine warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestMarineWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "Special Marine Warning" ||
						alert == "Marine Weather Statement" ||
						alert == "Hurricane Force Wind Warning" ||
						alert == "Hurricane Force Wind Watch" ||
						alert == "Storm Warning" ||
						alert == "Storm Watch" ||
						alert == "Gale Warning" ||
						alert == "Gale Watch" ||
						alert == "Small Craft Advisory" ||
						alert == "Marine Dense Fog Advisory";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active tropical warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestTropicalWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "Hurricane Warning" ||
						alert == "Hurricane Watch" ||
						alert == "Tropical Storm Warning" ||
						alert == "Tropical Storm Watch" ||
						alert == "Extreme Wind Warning" ||
						alert == "Hurricane Statement" ||
						alert == "Tropical Cyclone Statement";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active winter weather warnings from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestWinterWeatherWarnings()
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered =
						alert == "Wind Chill Warning" ||
						alert == "Wind Chill Watch" ||
						alert == "Wind Chill Advisory" ||
						alert == "Winter Storm Warning" ||
						alert == "Ice Storm Warning" ||
						alert == "Winter Storm Watch" ||
						alert == "Winter Weather Advisory" ||
						alert == "Blizzard Warning";
					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}

				}

				return [.. stormPredictionCenterWarnings];
			}

			/// <summary>
			/// Gets currently active warnings from the listed filter from the National Weather Service.
			/// </summary>
			/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
			public StormPredictionCenterWarning[] getLatestWarnings(string[] filter)
			{
				List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/alerts/active")) is not JObject data) return [.. stormPredictionCenterWarnings];
				foreach (var feature in data["features"])
				{
					string alert = (string)feature["properties"]["event"];
					if (alert == "Test Message") continue;

					bool isAlertRegistered = false;
					foreach (string filterEntry in filter)
						if (filterEntry == alert)
							isAlertRegistered = true;

					if (isAlertRegistered)
					{
						StormPredictionCenterWarning stormPredictionCenterWarning = new()
						{
							warningName = alert
						};

						// get the geometry for the warning
						if (feature["geometry"].ToString().Length > 0) // hacky way to check null geometry (using != null raises an exception)
						{
							SPCPolygon polygon = new();
							foreach (var coordinate in feature["geometry"]["coordinates"][0])
								polygon.coordinates.Add([(double)coordinate[1], (double)coordinate[0]]);
							stormPredictionCenterWarning.polygon = polygon;
						}

						// load times
						string sent = (string)feature["properties"]["sent"];
						string effective = (string)feature["properties"]["effective"];
						string onset = (string)feature["properties"]["onset"];
						string expires = (string)feature["properties"]["expires"];
						string ends = (string)feature["properties"]["ends"];
						if (sent != null)
							stormPredictionCenterWarning.sent = DateTime.Parse(sent);
						if (effective != null)
							stormPredictionCenterWarning.effective = DateTime.Parse(effective);
						if (onset != null)
							stormPredictionCenterWarning.onset = DateTime.Parse(onset);
						if (expires != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(expires);
						if (ends != null)
							stormPredictionCenterWarning.expires = DateTime.Parse(ends);

						// other data
						string senderName = (string)feature["properties"]["senderName"];
						string headline = (string)feature["properties"]["headline"];
						string description = (string)feature["properties"]["description"];
						string instruction = (string)feature["properties"]["instruction"];
						var nwsHeadline = feature["properties"]["parameters"]["NWSHeadline"];
						var windThreat = feature["properties"]["parameters"]["windThreat"];
						var maxWindGust = feature["properties"]["parameters"]["maxWindGust"];
						var hailThreat = feature["properties"]["parameters"]["hailThreat"];
						var maxHailSize = feature["properties"]["parameters"]["maxHailSize"];
						var tornadoDetection = feature["properties"]["parameters"]["tornadoDetection"];
						var thunderstormDamageThreat = feature["properties"]["parameters"]["thunderstormDamageThreat"];
						var cmamText = feature["properties"]["parameters"]["CMAMtext"];
						var cmamLongText = feature["properties"]["parameters"]["CMAMlongtext"];
						var flashFloodDetection = feature["properties"]["parameters"]["flashFloodDetection"];
						var flashFloodDamageThreat = feature["properties"]["parameters"]["flashFloodDamageThreat"];
						if (senderName != null)
							stormPredictionCenterWarning.sender = senderName;
						if (headline != null)
							stormPredictionCenterWarning.headline = headline;
						if (description != null)
							stormPredictionCenterWarning.description = description;
						if (instruction != null)
							stormPredictionCenterWarning.instruction = instruction;
						if (nwsHeadline != null)
							stormPredictionCenterWarning.nwsHeadline = (string)nwsHeadline[0];
						if (windThreat != null)
							stormPredictionCenterWarning.windThreat = (string)windThreat[0];
						if (maxWindGust != null)
							stormPredictionCenterWarning.maxWindGust = (string)maxWindGust[0];
						if (hailThreat != null)
							stormPredictionCenterWarning.hailThreat = (string)hailThreat[0];
						if (maxHailSize != null)
							stormPredictionCenterWarning.maxHailSize = (string)maxHailSize[0];
						if (tornadoDetection != null)
							stormPredictionCenterWarning.tornadoDetection = (string)tornadoDetection[0];
						if (thunderstormDamageThreat != null)
							stormPredictionCenterWarning.thunderstormDamageThreat = (string)thunderstormDamageThreat[0];
						if (cmamText != null)
							stormPredictionCenterWarning.cmamText = (string)cmamText[0];
						if (cmamLongText != null)
							stormPredictionCenterWarning.cmamLongText = (string)cmamLongText[0];
						if (flashFloodDetection != null)
							stormPredictionCenterWarning.flashFloodDetection = (string)flashFloodDetection[0];
						if (flashFloodDamageThreat != null)
							stormPredictionCenterWarning.flashFloodDamageThreat = (string)flashFloodDamageThreat[0];
						stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
					}
				}

				return [.. stormPredictionCenterWarnings];
			}
		}

		/// <summary>
		/// Archive data that has been processed by this API by saving it to a file in GeoJSON format.
		/// </summary>
		/// <remarks>This will be updated soon in future versions to cover the new data that can be gathered.</remarks>
		public class Archive
		{
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

						Feature feature = new();
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

		public class NHC
		{
			/// <summary>
			/// Gets the currently active storms in the Atlantic and Pacific from the National Hurricane Center.
			/// </summary>
			/// <returns>An array containing <see cref="StormObject"/> class which contains the processed data in a more easy to use format.</returns>
			public StormObject[] getActiveStorms()
			{
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
				List<StormObject> nationalHurricaneCenterActiveStorms = [];

				string temp = Path.GetTempPath();
				if (!Directory.Exists(temp + "/nhc data"))
					Directory.CreateDirectory(temp + "/nhc data");

				string fileName = temp + "/nhc_active.kml";
				Utils.downloadFile("https://www.nhc.noaa.gov/gis/kml/nhc_active.kml", fileName);

				if (!Directory.Exists(temp + "/nhc data"))
					Directory.CreateDirectory(temp + "/nhc data");

				foreach (string file in Directory.GetFiles(temp + "/nhc data"))
					File.Delete(file);

				KmlFile kmlFile = KmlFile.Load(File.OpenRead(fileName));
				if (kmlFile.Root is Kml kml)
					if (kml.Feature is Document doc)
						foreach (var feature in doc.Features)
							if (feature is Folder folder)
							{
								if (!folder.Id.Contains("at") && !folder.Id.Contains("ep")) continue; // skip any folders that do not hold hurricane data
								StormObject stormObject = new();
								if (folder.ExtendedData != null)
								{
									foreach (var data in folder.ExtendedData.OtherData)
									{
										string value = data.InnerXml;
										switch (data.Name)
										{
											case "type":
												stormObject.type = value;
												break;
											case "name":
												stormObject.name = value;
												break;
											case "wallet":
												stormObject.wallet = value;
												break;
											case "centerLat":
												stormObject.centerLat = double.Parse(value);
												break;
											case "centerLon":
												stormObject.centerLng = double.Parse(value);
												break;
											case "dateTime":
												string timeZone = string.Empty;
												string timeOffset = string.Empty;
												foreach (KeyValuePair<string, string> pair in timeZoneConvert)
													if (value.Contains(pair.Key))
													{
														timeOffset = pair.Value;
														timeZone = pair.Key;
													}
												stormObject.dateTime = DateTime.Parse(value.Replace(timeZone, timeOffset));
												break;
											case "movement":
												stormObject.movement = value;
												break;
											case "minimumPressure":
												stormObject.minimumPressureMbar = int.Parse(value.Split(' ')[0]);
												break;
											case "maxSustainedWind":
												stormObject.maxSustainedWindsMph = int.Parse(value.Split(' ')[0]);
												break;
											case "headline":
												stormObject.headline = value;
												break;
										}
									}
									nationalHurricaneCenterActiveStorms.Add(stormObject);
								}

								// download other kmz files in this active storms kml file via the network links
								foreach (var subFeature in folder.Features)
									if (subFeature is NetworkLink networkLink)
									{
										if (networkLink.Name == "Past Track")
										{
											string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
											string trackFileName = (temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml").Replace("_best_track", string.Empty);
											Utils.downloadFile(networkLink.Link.Href.ToString(), trackFileNameKmz);

											ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

											foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
												if (Path.GetExtension(file) != ".kml")
													File.Delete(file);
												else
													File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

											string fileData = File.ReadAllText(trackFileName);
											string modifiedData = fileData.Replace("http://earth.google.com/kml/2.2", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
											File.WriteAllText(trackFileName, modifiedData);

											XmlDocument xmlDoc = new();
											xmlDoc.LoadXml(modifiedData);

											XmlNamespaceManager nsManager = new(xmlDoc.NameTable);
											nsManager.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

											KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
											if (trackKmlData.Root is Kml trackKml)
												if (trackKml.Feature is Document trackDoc)
												{
													foreach (var trackFeature in trackDoc.Features)
													{
														if (trackFeature is Folder trackFolder)
														{
															foreach (var subTrackFeature in trackFolder.Features)
															{
																if (subTrackFeature is Placemark placemark)
																{
																	ForecastPoint forecastPoint = new();
																	XmlElement? xmlElement = xmlDoc.SelectSingleNode($"//kml:Placemark[kml:name='{placemark.Name}']", nsManager) as XmlElement;
																	if (placemark.Geometry is SharpKml.Dom.Point point)
																	{
																		forecastPoint.latitude = point.Coordinate.Latitude;
																		forecastPoint.longitude = point.Coordinate.Longitude;
																	}
																	forecastPoint.stormName = xmlElement["stormName"].InnerText;
																	forecastPoint.stormNumber = int.Parse(xmlElement["stormNum"].InnerText);
																	forecastPoint.basin = xmlElement["basin"].InnerText;
																	forecastPoint.type = xmlElement["stormType"].InnerText;
																	forecastPoint.intensityKts = int.Parse(xmlElement["intensity"].InnerText);
																	forecastPoint.intensityMph = int.Parse(xmlElement["intensityMPH"].InnerText);
																	forecastPoint.intensityKmh = int.Parse(xmlElement["intensityKPH"].InnerText);
																	forecastPoint.minSeaLevelPressure = int.Parse(xmlElement["minSeaLevelPres"].InnerText);
																	forecastPoint.date = DateTime.ParseExact(xmlElement["dtg"].InnerText, "HHmm 'UTC' MMM dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
																	stormObject.pastTrack.Add(forecastPoint);
																}
															}
														}
													}
												}
										}
										else if (networkLink.Name == "Track Forecast")
										{
											string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
											string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
											Utils.downloadFile(networkLink.Link.Href.ToString(), trackFileNameKmz);

											ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

											foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
												if (Path.GetExtension(file) != ".kml")
													File.Delete(file);
												else
													File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

											KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
											if (trackKmlData.Root is Kml trackKml)
												if (trackKml.Feature is Document trackDoc)
												{
													foreach (var trackFeature in trackDoc.Features)
													{
														if (trackFeature is Folder trackFolder)
														{
															foreach (var subTrackFeature in trackFolder.Features)
															{
																if (subTrackFeature is Placemark placemark)
																{
																	ForecastPoint forecastPoint = new();
																	if (placemark.Geometry is LineString lineString)
																	{
																		foreach (Data data in placemark.ExtendedData.Data)
																			if (data.Name == "stormName")
																				forecastPoint.stormName = data.Value;
																			else if (data.Name == "stormType")
																				forecastPoint.type = data.Value;
																			else if (data.Name == "maxWindKnots")
																				forecastPoint.intensityKts = int.Parse(data.Value);
																			else if (data.Name == "maxWindMPH")
																				forecastPoint.intensityMph = int.Parse(data.Value);
																			else if (data.Name == "maxGustKnots")
																				forecastPoint.windGustsKts = int.Parse(data.Value);
																			else if (data.Name == "maxGustMPH")
																				forecastPoint.windGustsMph = int.Parse(data.Value);
																		foreach (Vector vec in lineString.Coordinates)
																		{
																			forecastPoint.latitude = vec.Latitude;
																			forecastPoint.longitude = vec.Longitude;
																			break;
																		}
																		stormObject.forecastPoints.Add(forecastPoint);
																	}
																	if (placemark.Geometry is SharpKml.Dom.Point point)
																	{
																		forecastPoint.latitude = point.Coordinate.Latitude;
																		forecastPoint.longitude = point.Coordinate.Longitude;
																		string[] trimmedDesc = Regex.Replace(placemark.Description.Text, "</?(tr|td|font|table|b|font color=black|td nowrap|hr)>", string.Empty).Trim().Replace("\t", string.Empty).Split('\n');
																		string stormName = trimmedDesc[0];
																		string forecastHour = trimmedDesc[3].Trim();
																		//DateTime validAt = DateTime.Parse(trimmedDesc[4].Split("Valid at:")[1]);
																		string[] winds = trimmedDesc[6].Split("Maximum Wind:")[1].Split('(');
																		int maxWindsKts = int.Parse(winds[0].Replace("knots", string.Empty).Trim());
																		int maxWindsMph = int.Parse(winds[1].Replace("mph)", string.Empty).Trim());
																		string[] windGusts = trimmedDesc[7].Split("Wind Gusts:")[1].Split('(');
																		int maxWindGustsKts = int.Parse(windGusts[0].Replace("knots", string.Empty).Trim());
																		int maxWindGustsMph = int.Parse(windGusts[1].Replace("mph)", string.Empty).Trim());
																		forecastPoint.stormName = stormName;
																		forecastPoint.forecastHour = forecastHour;
																		//forecastPoint.date = validAt;
																		forecastPoint.intensityKts = maxWindsKts;
																		forecastPoint.intensityMph = maxWindsMph;
																		forecastPoint.windGustsKts = maxWindGustsKts;
																		forecastPoint.windGustsMph = maxWindGustsMph;
																		bool isHurricane = stormName.Contains("Hurricane");
																		bool isStorm = stormName.Contains("Storm");
																		bool isDepression = stormName.Contains("Depression");
																		string stormType = placemark.StyleUrl.ToString();
																		if (stormType == "#initial_point")
																			if (isHurricane)
																				forecastPoint.type = "Hurricane";
																			else if (isStorm)
																				forecastPoint.type = "Storm";
																			else if (isDepression)
																				forecastPoint.type = "Depression";
																		if (stormType == "#m_point")
																			forecastPoint.type = "Major Hurricane";
																		else if (stormType == "#h_point")
																			forecastPoint.type = "Hurricane";
																		else if (stormType == "#s_point")
																			forecastPoint.type = "Storm";
																		else if (stormType == "#d_point")
																			forecastPoint.type = "Depression";
																		else if (stormType == "#l_point")
																			forecastPoint.type = "Low";
																		else if (stormType == "#xm_point" || stormType == "#xh_point" || stormType == "#xs_point" || stormType == "#xd_point")
																			forecastPoint.type = "Post/Potential Tropical Cyclone";
																		stormObject.forecastPoints.Add(forecastPoint);
																	}
																}
															}
														}
													}
												}
										}
										else if (networkLink.Name == "Cone of Uncertainty")
										{
											string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
											string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
											Utils.downloadFile(networkLink.Link.Href.ToString(), trackFileNameKmz);

											ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

											foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
												if (Path.GetExtension(file) != ".kml")
													File.Delete(file);
												else
													File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

											string fileData = File.ReadAllText(trackFileName);
											string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
											File.WriteAllText(trackFileName, modifiedData);

											KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
											if (trackKmlData.Root is Kml trackKml)
												if (trackKml.Feature is Document trackDoc)
												{
													foreach (var trackFeature in trackDoc.Features)
													{
														if (trackFeature is Placemark placemark)
														{
															ForecastCone forecastCone = new();
															if (placemark.Geometry is Polygon polygon)
																foreach (Vector vec in polygon.OuterBoundary.LinearRing.Coordinates)
																	forecastCone.coordinates.Add([vec.Latitude, vec.Longitude]);
															foreach (Data data in placemark.ExtendedData.Data)
															{
																if (data.Name == "stormType")
																	forecastCone.type = data.Value;
																else if (data.Name == "stormName")
																	forecastCone.stormName = data.Value;
																else if (data.Name == "stormNum")
																	forecastCone.stormNumber = int.Parse(data.Value);
																else if (data.Name == "advisoryNum")
																	forecastCone.advisoryNumber = data.Value;
																else if (data.Name == "basin")
																	forecastCone.basin = data.Value;
																else if (data.Name == "advisoryDate")
																{
																	string timeZone = string.Empty;
																	string timeOffset = string.Empty;
																	foreach (KeyValuePair<string, string> pair in timeZoneConvert)
																		if (data.Value.Contains(pair.Key))
																		{
																			timeOffset = pair.Value;
																			timeZone = pair.Key;
																		}
																	string restructured = string.Empty;
																	string[] split = data.Value.Split(' ');
																	string time = string.Empty;
																	if (split[0].Length == 3)
																		time = split[0].Insert(1, ":");
																	else if (split[0].Length == 4)
																		time = split[0].Insert(2, ":");
																	restructured = $"{time} {split[1]} {split[2].Replace(timeZone, timeOffset)} {split[3]} {split[4]} {split[5]} {split[6]}";
																	forecastCone.advisoryDate = DateTime.ParseExact(restructured, "h:mm tt zzz ddd MMM dd yyyy", CultureInfo.InvariantCulture);
																}
																stormObject.forecastCone = forecastCone;
															}
														}
													}
												}
										}
										else if (networkLink.Name == "Initial Extent of Winds")
										{
											string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
											string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
											Utils.downloadFile(networkLink.Link.Href.ToString(), trackFileNameKmz);

											ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

											foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
												if (Path.GetExtension(file) != ".kml")
													File.Delete(file);
												else
													File.Move(file, temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml");

											KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
											if (trackKmlData.Root is Kml trackKml)
												if (trackKml.Feature is Document trackDoc)
													foreach (var subTrackFeature in trackDoc.Features)
														if (subTrackFeature is Folder trackFolder)
															foreach (var trackFeature in trackFolder.Features)
															{
																if (trackFeature is Placemark placemark)
																{
																	WindRadii windRadii = new();
																	if (placemark.Geometry is Polygon polygon)
																		foreach (Vector vec in polygon.OuterBoundary.LinearRing.Coordinates)
																			windRadii.coordinates.Add([vec.Latitude, vec.Longitude]);
																	stormObject.WindRadii.Add(windRadii);
																}
															}
										}
										foreach (string file in Directory.GetFiles(temp + "/nhc data"))
											File.Delete(file);
									}
							}

				return [.. nationalHurricaneCenterActiveStorms];
			}

			/// <summary>
			/// Gets the active storm specified in the Atlantic and Pacific from the National Hurricane Center.
			/// </summary>
			/// <remarks>This calls <see cref="getActiveStorms"/> internally.</remarks>
			/// <returns>A <see cref="StormObject"/> class object containing data about the storm.</returns>
			public StormObject? getActiveStorm(string name)
			{
				StormObject[] activeStorms = getActiveStorms();
				foreach (StormObject storm in activeStorms)
				{
					if (storm.name == name)
						return storm;
				}
				return null;
			}

			/// <summary>
			/// Gets the active storm specified in the Atlantic and Pacific from the National Hurricane Center.
			/// </summary>
			/// <remarks>This version does not call <see cref="getActiveStorms"/> internally. Instead, letting you call <see cref="getActiveStorms"/>, and using that to search through (puts ease on internet connections).</remarks>
			/// <returns>A <see cref="StormObject"/> class object containing data about the storm.</returns>
			public StormObject? getActiveStorm(string name, StormObject[] activeStorms)
			{
				foreach (StormObject storm in activeStorms)
				{
					if (storm.name == name)
						return storm;
				}
				return null;
			}

			/// <summary>
			/// Gets currently active disturbances in the Atlantic or Pacific. (accepted basins: Atlantic, East Pacific, Central Pacific)
			/// </summary>
			/// <param name="basin">The basin to get disturbances from.</param>
			/// <returns>An array containing <see cref="DisturbanceObject"/> objects.</returns>
			public DisturbanceObject[] getDisturbances(string basin)
			{
				List<DisturbanceObject> nationalHurricaneCenterDisturbances = [];

				string temp = Path.GetTempPath();
				if (!Directory.Exists(temp + "/nhc data"))
					Directory.CreateDirectory(temp + "/nhc data");

				foreach (string file in Directory.GetFiles(temp + "/nhc data"))
					File.Delete(file);

				if (basin == "Atlantic")
				{
					string fileName = temp + "/nhc data/nhc_disturbances.kmz";
					string kmlFileName = temp + "/nhc data/gtwo_atl.kml";
					Utils.downloadFile("https://www.nhc.noaa.gov/xgtwo/gtwo_atl.kmz", fileName);

					if (!Directory.Exists(temp + "/nhc data"))
						Directory.CreateDirectory(temp + "/nhc data");

					ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

					string fileData = File.ReadAllText(kmlFileName);
					string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
					File.WriteAllText(kmlFileName, modifiedData);

					KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
					if (kmlFile.Root is Kml kml)
						if (kml.Feature is Document doc)
							foreach (var feature in doc.Features)
								if (feature is Placemark placemark)
								{
									DisturbanceObject disturbance = new();
									if (placemark.Geometry is Polygon polygon)
									{
										SPCPolygon p = new();
										foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
											p.coordinates.Add([g.Latitude, g.Longitude]);
										disturbance.polygon = p;
									}
									else if (placemark.Geometry is SharpKml.Dom.Point point)
										disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
									foreach (Data data in placemark.ExtendedData.Data)
										if (data.Name == "Disturbance")
											disturbance.disturbanceIndex = byte.Parse(data.Value);
										else if (data.Name == "2day_percentage")
											disturbance.day2Percentage = data.Value;
										else if (data.Name == "2day_category")
											disturbance.day2Category = data.Value;
										else if (data.Name == "7day_percentage")
											disturbance.day7Percentage = data.Value;
										else if (data.Name == "7day_category")
											disturbance.day7Category = data.Value;
										else if (data.Name == "Discussion")
											disturbance.discussion = data.Value;
									nationalHurricaneCenterDisturbances.Add(disturbance);
								}
				}
				else if (basin == "East Pacific")
				{
					string fileName = temp + "/nhc data/nhc_disturbances.kmz";
					string kmlFileName = temp + "/nhc data/gtwo_pac.kml";
					Utils.downloadFile("https://www.nhc.noaa.gov/xgtwo/gtwo_pac.kmz", fileName);

					if (!Directory.Exists(temp + "/nhc data"))
						Directory.CreateDirectory(temp + "/nhc data");

					ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

					string fileData = File.ReadAllText(kmlFileName);
					string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
					File.WriteAllText(kmlFileName, modifiedData);

					KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
					if (kmlFile.Root is Kml kml)
						if (kml.Feature is Document doc)
							foreach (var feature in doc.Features)
								if (feature is Placemark placemark)
								{
									DisturbanceObject disturbance = new();
									if (placemark.Geometry is Polygon polygon)
									{
										SPCPolygon p = new();
										foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
											p.coordinates.Add([g.Latitude, g.Longitude]);
										disturbance.polygon = p;
									}
									else if (placemark.Geometry is SharpKml.Dom.Point point)
										disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
									foreach (Data data in placemark.ExtendedData.Data)
										if (data.Name == "Disturbance")
											disturbance.disturbanceIndex = byte.Parse(data.Value);
										else if (data.Name == "2day_percentage")
											disturbance.day2Percentage = data.Value;
										else if (data.Name == "2day_category")
											disturbance.day2Category = data.Value;
										else if (data.Name == "7day_percentage")
											disturbance.day7Percentage = data.Value;
										else if (data.Name == "7day_category")
											disturbance.day7Category = data.Value;
										else if (data.Name == "Discussion")
											disturbance.discussion = data.Value;
									nationalHurricaneCenterDisturbances.Add(disturbance);
								}
				}
				else if (basin == "Central Pacific")
				{
					string fileName = temp + "/nhc data/nhc_disturbances.kmz";
					string kmlFileName = temp + "/nhc data/gtwo_cpac.kml";
					Utils.downloadFile("https://www.nhc.noaa.gov/xgtwo/gtwo_cpac.kmz", fileName);

					if (!Directory.Exists(temp + "/nhc data"))
						Directory.CreateDirectory(temp + "/nhc data");

					ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

					string fileData = File.ReadAllText(kmlFileName);
					string modifiedData = fileData.Replace("<kml xmlns:gx=\"http://www.google.com/kml/ext/2.2\" xmlns=\"http://earth.google.com/kml/2.1\">", "<kml xmlns=\"http://www.opengis.net/kml/2.2\">"); // sharpkml doesnt like googles kml namespace
					modifiedData = modifiedData.Replace("gx:", string.Empty);
					File.WriteAllText(kmlFileName, modifiedData);

					KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
					if (kmlFile.Root is Kml kml)
						if (kml.Feature is Document doc)
							foreach (var feature in doc.Features)
								if (feature is Placemark placemark)
								{
									if (placemark.ExtendedData == null) continue;
									DisturbanceObject disturbance = new();
									if (placemark.Geometry is Polygon polygon)
									{
										SPCPolygon p = new();
										foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
											p.coordinates.Add([g.Latitude, g.Longitude]);
										disturbance.polygon = p;
									}
									else if (placemark.Geometry is SharpKml.Dom.Point point)
										disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
									foreach (Data data in placemark.ExtendedData.Data)
										if (data.Name == "Disturbance")
											disturbance.disturbanceIndex = byte.Parse(data.Value);
										else if (data.Name == "2day_percentage")
											disturbance.day2Percentage = data.Value;
										else if (data.Name == "2day_category")
											disturbance.day2Category = data.Value;
										else if (data.Name == "7day_percentage")
											disturbance.day7Percentage = data.Value;
										else if (data.Name == "7day_category")
											disturbance.day7Category = data.Value;
										else if (data.Name == "Discussion")
											disturbance.discussion = data.Value;
									nationalHurricaneCenterDisturbances.Add(disturbance);
								}
				}
				return [.. nationalHurricaneCenterDisturbances];
			}
		}

		/// <summary>
		/// Gather radar stations and radar images (and soon raw radar data) from the NWS.
		/// </summary>
		public class Radar
		{
			/// <summary>
			/// Returns a class that contains all of the registered radar stations.
			/// </summary>
			/// <remarks>
			/// Because radar stations are static, they are unlikely to change often, and therefore, it would be better to cache the radar stations and only load them upon your program loading.
			/// </remarks>
			/// <returns>An array <see cref="RadarStation"/> class containing all the registered radar stations.</returns>
			public RadarStation[] getRadarStations()
			{
				List<RadarStation> radarStations = [];
				if (JsonConvert.DeserializeObject(Utils.downloadString("https://api.weather.gov/radar/stations")) is JObject json)
					foreach (var feature in json["features"])
					{
						RadarStation radarStation = new()
						{
							id = (string)feature["properties"]["id"],
							name = (string)feature["properties"]["name"],
							stationType = (string)feature["properties"]["stationType"]
						};
						SPCPoint point = new((double)feature["geometry"]["coordinates"][1], (double)feature["geometry"]["coordinates"][0]);
						radarStation.location = point;
						Elevation elevation = new()
						{
							unit = (string)feature["properties"]["elevation"]["unitCode"],
							elevation = (double)feature["properties"]["elevation"]["value"]
						};
						radarStation.elevation = elevation;
						radarStation.timeZone = (string)feature["properties"]["timeZone"];
						if (feature["properties"]["rda"].ToString().Length > 0)
							radarStation.mode = (string)feature["properties"]["rda"]["properties"]["mode"];
						radarStations.Add(radarStation);
					}
				return [.. radarStations];
			}

			/// <summary>
			/// Gets a pre generated radar image from RIDGE II.
			/// </summary>
			/// <remarks>This method will be deprecated soon in favor of a binary radar decoder and image generator in future versions.</remarks>
			/// <param name="radarStation">The radar station.</param>
			/// <param name="product">The radar product.</param>
			/// <returns>The file path for the downloaded radar image.</returns>
			public string getRidge2RadarImage(string radarStation, RadarProduct product)
			{
				string url = $"https://mrms.ncep.noaa.gov/data/RIDGEII/L3/{radarStation}/{product}/";
				string pngFile = Path.Combine(Environment.CurrentDirectory, "temp", $".png");
				string filePath = Path.Combine(Environment.CurrentDirectory, "temp", ".gz");
				string tifFile = Path.Combine(Environment.CurrentDirectory, "temp", ".tif");

				try
				{
					// retrieve file url and download
					List<string> radarImages = Utils.getUrlsFromWebpage(url);
					if (radarImages == null || radarImages.Count == 0)
						throw new Exception("No radar images found on the webpage.");

					string latestRadarImage = url + radarImages.Last();
					pngFile = Environment.CurrentDirectory + "\\temp\\" + Path.GetFileName(pngFile).Replace(".png", radarImages.Last() + ".png");
					filePath = Environment.CurrentDirectory + "\\temp\\" + Path.GetFileName(filePath).Replace(".gz", radarImages.Last() + ".gz");
					tifFile = Environment.CurrentDirectory + "\\temp\\" + Path.GetFileName(tifFile).Replace(".tif", radarImages.Last() + ".tif");
					Utils.downloadFile(latestRadarImage, filePath);

					// unzip the file
					using (Stream newFile = File.Create(tifFile))
					using (FileStream fileStream = File.OpenRead(filePath))
					using (GZipStream stream = new(fileStream, CompressionMode.Decompress))
						stream.CopyTo(newFile);

					// convert TIF to PNG
					if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
					{
						using Image image = Image.FromFile(tifFile);
						image.Save(pngFile, ImageFormat.Png);
					}
					else
					{
						throw new("Versions of Windows below version 6.1 does not support the 'Image' API. Upgrade to Windows 7 and beyond to resolve.");
					}

					// delete intermediate files
					File.Delete(filePath);
					File.Delete(tifFile);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"An error occurred: {ex.Message}");
					return string.Empty;
				}

				return pngFile;
			}
		}

		// TODO: add an event that fires when watches are updated. also add events for newly issued mesoscale discussions, warnings, and tropical cyclones
		public class Events
		{
			public delegate void WatchIssuedEventHandler(object sender, StormPredictionCenterWatch[] watches, StormPredictionCenterWatchBox[] watchBoxes);

			/// <summary>
			/// Fired whenever the Storm Prediction Center issues a tornado/severe thunderstorm watch. (untested as of 1.0.2, need verification it works)
			/// </summary>
			public event WatchIssuedEventHandler watchIssued;

			/// <summary>
			/// Allows events to be enabled.
			/// </summary>
			public void enableEvents()
			{
				timer.Resume();
				eventsEnabled = true;
			}

			/// <summary>
			/// Allows events to be disabled.
			/// </summary>
			public void disableEvents()
			{
				timer.Pause();
				eventsEnabled = false;
			}

			private readonly ExtendedTimer timer = new();
			private bool eventsEnabled = false;
			private readonly Watches watches = new();
			private readonly List<int> lastWatches = [];
			public Events()
			{
				timer.TickInterval = 4000;
				timer.TickOnStart = true;
				timer.OnTimerTick += (sender, e) =>
				{
					if (!eventsEnabled) return;
					StormPredictionCenterWatch[] tornadoWatches = watches.getActiveTornadoWatches();
					StormPredictionCenterWatch[] severeThunderstormWatches = watches.getActiveSevereThunderstormWatches();
					StormPredictionCenterWatchBox[] watchBoxes = watches.getActiveWatchBoxes();

					List<StormPredictionCenterWatch> theNewWatch = [];
					foreach (StormPredictionCenterWatch watch in tornadoWatches)
					{
						if (!lastWatches.Contains(watch.watchNumber))
						{
							theNewWatch.Add(watch);
							lastWatches.Add(watch.watchNumber);
						}
					}

					List<StormPredictionCenterWatchBox>? theNewWatchBox = [];
					foreach (StormPredictionCenterWatchBox watchBox in watchBoxes)
						if (!lastWatches.Contains(watchBox.watchNumber))
							theNewWatchBox.Add(watchBox);

					// refire check: dont call this event again if the found watches match the last ones stored
					foreach (StormPredictionCenterWatch watch in tornadoWatches)
						if (timer.TimeSinceStart < watch.sent.Ticks) // watch has been issued after we started listening for events
							lastWatches.Add(watch.watchNumber);

					foreach (StormPredictionCenterWatch watch in severeThunderstormWatches)
						if (timer.TimeSinceStart < watch.sent.Ticks) // watch has been issued after we started listening for events
							lastWatches.Add(watch.watchNumber);

					if (theNewWatch.Count > 0)
						watchIssued?.Invoke(this, [.. theNewWatch], [.. theNewWatchBox]);
				};
				timer.Start();
			}
		}

		public StormPredictionCenter()
		{
			outlooks = new();
			watches = new();
			warnings = new();
			archive = new();
			nhc = new();
			radar = new();
			events = new();
		}
	}

	/// <summary>
	/// Util methods for this class. These methods are not intended to be accessed outside this script, but they are public anyway. (may change in future versions)
	/// </summary>
	public class Utils
	{
		public static int getSevereThunderstormWatchNumber(string text)
		{
			string[] split = text.Replace('\n', ' ').Split(' ');
			string matchingWord = string.Empty;
			foreach (string word in split)
			{
				string lower = word.ToLower();

				// we found a match, that means the next word will be the watch number
				if (matchingWord == "severe thunderstorm watch ")
					return int.Parse(word.Where(char.IsDigit).ToArray());

				// check if "lower" matches to any of these words, if it does, concat it to the matchingWord string variable
				if (lower == "severe" || lower == "thunderstorm" || lower == "watch")
					matchingWord += lower + " ";
			}
			return 0;
		}

		public static int getTornadoWatchNumber(string text)
		{
			string[] split = text.Replace('\n', ' ').Split(' ');
			string matchingWord = string.Empty;
			foreach (string word in split)
			{
				string lower = word.ToLower();

				// we found a match, that means the next word will be the watch number
				if (matchingWord == "tornado watch ")
					return int.Parse(word.Where(char.IsDigit).ToArray());

				// check if "lower" matches to any of these words, if it does, concat it to the matchingWord string variable
				if (lower == "tornado" || lower == "watch")
					matchingWord += lower + " ";
			}
			return 0;
		}

		// obsolete. remove later
		[Obsolete]
		public static (bool, string) doesOutlookTimeExist(string url, int day)
		{
			// days beyond 3 only have a single outlook with no specified time, hence why we are not bothering with day 4+ outlooks
			if (day == 1)
			{
				// the times are in a reversed order since the smallest times have risks that come first, and simplifies the check
				string[] timesDay = ["2000", "1630", "1300", "1200", "0100"];
				for (int i = 0; i < 5; i++)
				{
					string newUrl = url.Replace("TIME", timesDay[i]);
					string data = downloadString(newUrl);
					if (data != string.Empty)
						return (data != string.Empty, timesDay[i]);
				}
			}
			else if (day == 2)
			{
				string[] timesDay = ["1730", "0600"];
				for (int i = 0; i < 2; i++)
				{
					string newUrl = url.Replace("TIME", timesDay[i]);
					string data = downloadString(newUrl);
					if (data != string.Empty)
						return (data != string.Empty, timesDay[i]);
				}
			}
			else if (day == 3)
			{
				// TODO: simplify this because day 3 categorical outlooks only have a single time
				string[] timesDay = ["0730"];
				for (int i = 0; i < 1; i++)
				{
					string newUrl = url.Replace("TIME", timesDay[i]);
					string data = downloadString(newUrl);
					if (data != string.Empty)
						return (data != string.Empty, timesDay[i]);
				}
			}

			return (false, string.Empty);
		}

		public static bool isValidOutlook(string url)
		{
			try
			{
				downloadString(url); // this will raise an exception because of a 502 error if its not valid
			}
			catch
			{
				return false;
			}
			return true;
		}

		public static Color hexToRgb(string hex)
		{
			if (string.IsNullOrEmpty(hex)) return Color.White;
			if (hex.StartsWith('#'))
				hex = hex[1..];

			int intValue = int.Parse(hex, NumberStyles.HexNumber);
			int red = (intValue >> 16) & 0xFF;
			int green = (intValue >> 8) & 0xFF;
			int blue = intValue & 0xFF;
			return Color.FromArgb(red, green, blue);
		}

		public static string downloadString(string url)
		{
			using HttpClient http = new();
			http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
			try
			{
				return http.GetStringAsync(url).Result;
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void downloadFile(string url, string fileName)
		{
			using HttpClient http = new();
			http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
			Stream stream = http.GetStreamAsync(url).Result;
			using FileStream fs = new(fileName, FileMode.Create);
			stream.CopyTo(fs);
			fs.Close();
			stream.Close();
		}

		/// <summary>
		/// Adds a space in between words that start with a capital letter.
		/// </summary>
		/// <remarks>
		/// Example:
		/// <code>
		/// Debug.WriteLine(spaceOut("ThisIsATestString"));
		/// >> This Is A Test String;
		/// </code>
		/// </remarks>
		/// <returns>A version of the string with words with capitalization spaced out.</returns>
		public static string spaceOut(string str)
		{
			string newStr = string.Empty;
			foreach (char c in str)
				if (!char.IsUpper(c))
					newStr += c;
				else
					newStr += " " + c;
			return newStr[1..];
		}

		public static List<string> getUrlsFromWebpage(string url)
		{
			List<string> urls = [];
			using HttpClient httpClient = new();
			try
			{
				string htmlContent = httpClient.GetStringAsync(url).Result;
				HtmlDocument htmlDocument = new();
				htmlDocument.LoadHtml(htmlContent);
				extractLinks(htmlDocument.DocumentNode, urls);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error fetching or parsing the webpage: {ex.Message}");
			}

			return urls;
		}

		public static void extractLinks(HtmlNode node, List<string> urls)
		{
			if (node.Name == "a" && node.HasAttributes && node.Attributes.Contains("href"))
			{
				string link = node.Attributes["href"].Value;
				urls.Add(link);
			}

			foreach (var childNode in node.ChildNodes)
				extractLinks(childNode, urls);
		}
	}
}
