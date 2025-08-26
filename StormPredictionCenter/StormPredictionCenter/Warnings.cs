using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using GeoJSON.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Azrellie.Meteorology.SPC;
// TODO: download county geometry from county wide alerts
/// <summary>
/// Retrieve warning data from the NWS.
/// </summary>
public class Warnings(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	/// <summary>
	/// Gets currently active convective warnings from the National Weather Service.
	/// </summary>
	/// <param name="includeCustomWarnings">
	/// Whether or not custom warnings should be included. Custom warnings are variations of the same warning displayed in their own type of warning.
	/// An example would be a tornado warning with "tornado emergency" in its bulletin being displayed as a tornado emergency instead. Or a severe thunderstorm warning that meets a certian criteria being displayed as a "derecho warning" (EXPERIMENTAL).
	/// </param>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestConvectiveWarnings(bool includeCustomWarnings = false)
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection? data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=tornado%20warning,severe%20thunderstorm%20warning,special%20weather%20statement,severe%20weather%20statement,tornado%20watch,severe%20thunderstorm%20watch"));
		if (data.Features == null) return [];
		if (data.Features.Count == 0) return [];
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				double maxWindGust = -1;
				double maxHailSize = -1;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "windThreat")
						stormPredictionCenterWarning.windThreat = (string)property.Value[0];
					else if (property.Name == "maxWindGust")
					{
						string[] split = ((string)property.Value[0]).Split(' ');
						if (split.Length > 2)
						{
							maxWindGust = double.Parse(split[2]);
							stormPredictionCenterWarning.maxWindGust = double.Parse(split[2]);
						}
						else
						{
							maxWindGust = double.Parse(split[0]);
							stormPredictionCenterWarning.maxWindGust = double.Parse(split[0]);
						}
					}
					else if (property.Name == "hailThreat")
						stormPredictionCenterWarning.hailThreat = (string)property.Value[0];
					else if (property.Name == "maxHailSize")
					{
						string[] split = ((string)property.Value[0]).Split(' ');
						if (split.Length > 1)
						{
							maxHailSize = double.Parse(split[2]);
							stormPredictionCenterWarning.maxHailSize = double.Parse(split[2]);
						}
						else
						{
							maxHailSize = double.Parse(split[0]);
							stormPredictionCenterWarning.maxHailSize = double.Parse(split[0]);
						}
					}
					else if (property.Name == "tornadoDetection")
						stormPredictionCenterWarning.tornadoDetection = (string)property.Value[0];
					else if (property.Name == "waterspoutDetection")
						stormPredictionCenterWarning.waterspoutDetection = (string)property.Value[0];
					else if (property.Name == "tornadoDamageThreat")
						stormPredictionCenterWarning.tornadoDamageThreat = (string)property.Value[0];
					else if (property.Name == "thunderstormDamageThreat")
						stormPredictionCenterWarning.thunderstormDamageThreat = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
				if (includeCustomWarnings)
					if (alert == "Tornado Warning" && description.Contains("tornado emergency", StringComparison.OrdinalIgnoreCase))
						stormPredictionCenterWarning.warningName = "Tornado Emergency";
					else if (alert == "Tornado Warning" && description.Contains("particularly dangerous situation", StringComparison.OrdinalIgnoreCase))
						stormPredictionCenterWarning.warningName = "PDS Tornado Warning";
					else if (alert == "Severe Thunderstorm Warning")
					{
						double stormSpeedMph = double.Parse(((string)parameters["eventMotionDescription"][0]).Split("...")[3].Replace("KT", string.Empty)) * 1.151;
						if (cmamLongText != null && cmamLongText.Contains("along a line", StringComparison.OrdinalIgnoreCase) && maxWindGust >= 70 && maxHailSize < 1.25 && stormSpeedMph >= 50 && instruction.Contains("widespread wind damage", StringComparison.OrdinalIgnoreCase))
							stormPredictionCenterWarning.warningName = "Derecho Warning";
					}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active hydrologic warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestHydrologicWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=flash%20flood%20warning,flood%20warning,flash%20flood%20watch,flood%20advisory,flood%20watch,flood%20statement,hydrologic%20statement,hydrologic%20outlook,flash%20flood%20statement"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
					else if (property.Name == "flashFloodDetection")
						stormPredictionCenterWarning.flashFloodDetection = (string)property.Value[0];
					else if (property.Name == "flashFloodDamageThreat")
						stormPredictionCenterWarning.flashFloodDamageThreat = (string)property.Value[0];
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active non convective warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestNonConvectiveWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=high%20wind%20warning,high%20wind%20watch,%20wind%20advisory,excessive%20heat%20warning,%20heat%20advisory,dense%20fog%20advisory,dense%20smoke%20advisory,dust%20storm%20warning,blowing%20dust%20advisory,air%20stagnation%20advisory,air%20quality%20alert,freeze%20warning,freeze%20watch,hard%20freeze%20warning,hard%20freeze%20watch"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			string id = (string)feature.Properties["id"];
			if (id != null)
				stormPredictionCenterWarning.id = id;
			stormPredictionCenterWarning.sent = ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active marine warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestMarineWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=special%20marine%20warning,marine%20weather%20statement,hurricane%20force%20wind%20warning,hurricane%20force%20wind%20watch,storm%20warning,storm%20watch,gale%20warning,%20gale%20watch,small%20craft%20advisory,marine%20dense%20fog%20advisory"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			string id = (string)feature.Properties["id"];
			if (id != null)
				stormPredictionCenterWarning.id = id;
			stormPredictionCenterWarning.sent = ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "waterspoutDetection")
						stormPredictionCenterWarning.waterspoutDetection = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active tropical warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestTropicalWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=hurricane%20warning,hurricane%20watch,tropical%20storm%20warning,tropical%20storm%20watch,extreme%20wind%20warning,hurricane%20statement,tropical%20cyclone%20statement"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active winter weather warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestWinterWeatherWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=extreme%20cold%20warning,cold%20weather%20watch,cold%20weather%20advisory,winter%20storm%20warning,ice%20storm%20warning,winter%20storm%20watch,winter%20weather%20advisory,blizzard%20warning,heavy%20freezing%20spray%20warning,lake%20effect%20snow%20warning,snow%20squall%20warning"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active fire weather warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestFireWeatherWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=red%20flag%20warning,fire%20warning,fire%20weather%20watch,fire%20danger%20statement"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active coastal warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestCoastalWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=coastal%20flood%20warning,coastal%20flood%20watch,coastal%20flood%20advisory,coastal%20flood%20statement,high%20surf%20warning,high%20surf%20advisory,rip%20current%20statement"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active non weather warnings from the National Weather Service.
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestNonWeatherWarnings()
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString("https://api.weather.gov/alerts/active?event=civil%20emergency,child%20abduction%20emergency,civil%20danger%20warning,earthquake%20warning,local%20area%20emergency,law%20enforcement%20warning,911%20telephone%20outage,hazardous%20materials%20warning,nuclear%20hazard%20warning,radiological%20hazard%20warning,evacuation%20immediate,fire%20warning,shelter%20in%20place%20warning,volcano%20warning,tsunami%20warning,tsunami%20watch,tsunami%20advisory,tsunami%20information%20statement"));
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new();

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}

	/// <summary>
	/// Gets currently active warnings from the listed filter from the National Weather Service. Alert names are not case sensitive, but must have spaces to ensure proper filtering.
	/// <remarks>If the filter parameter is not specified, all active alerts will be retrieved.</remarks>
	/// </summary>
	/// <returns>An array <see cref="StormPredictionCenterWarning"/> class which contains the processed data in a more easy to use format.</returns>
	public StormPredictionCenterWarning[] getLatestWarnings(string[]? filter = null, bool includeCustomWarnings = false)
	{
		List<StormPredictionCenterWarning> stormPredictionCenterWarnings = [];
		string url = "https://api.weather.gov/alerts/active";
		if (filter != null)
		{
			url += "?event=";
			for (int i = 0; i < filter.Length; i++)
			{
				string alert = filter[i];
				url += alert.Replace(" ", "%20");
				if (i < filter.Length - 1)
					url += ",";
			}
		}
		FeatureCollection data = JsonConvert.DeserializeObject<FeatureCollection>(Utils.downloadString(url));
		if (data == null) return [];
		foreach (var feature in data.Features)
		{
			string alert = (string)feature.Properties["event"];
			StormPredictionCenterWarning stormPredictionCenterWarning = new()
			{
				warningName = alert
			};

			if (feature.Geometry != null)
			{
				if (feature.Geometry.Type is GeoJSONObjectType.Polygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as Polygon;
				else if (feature.Geometry.Type is GeoJSONObjectType.MultiPolygon)
					stormPredictionCenterWarning.polygon = feature.Geometry as MultiPolygon;
			}
			else
				stormPredictionCenterWarning.affectedCounties = feature.Properties["affectedZones"] as List<string>;

			// process times and other data
			stormPredictionCenterWarning.id = feature.Properties["id"] == null ? string.Empty : (string)feature.Properties["id"];
			stormPredictionCenterWarning.sent = feature.Properties["sent"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["sent"]).ToUniversalTime();
			stormPredictionCenterWarning.effective = feature.Properties["effective"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["effective"]).ToUniversalTime();
			stormPredictionCenterWarning.onset = feature.Properties["onset"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["onset"]).ToUniversalTime();
			stormPredictionCenterWarning.expires = feature.Properties["expires"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["expires"]).ToUniversalTime();
			stormPredictionCenterWarning.ends = feature.Properties["ends"] == null ? DateTime.MinValue : ((DateTime)feature.Properties["ends"]).ToUniversalTime();

			// other data
			string senderName = (string)feature.Properties["senderName"];
			string headline = (string)feature.Properties["headline"];
			string description = (string)feature.Properties["description"];
			string instruction = (string)feature.Properties["instruction"];
			string eventType = (string)feature.Properties["messageType"];
			if (senderName != null)
				stormPredictionCenterWarning.sender = senderName;
			if (headline != null)
				stormPredictionCenterWarning.headline = headline;
			if (description != null)
				stormPredictionCenterWarning.description = description;
			if (instruction != null)
				stormPredictionCenterWarning.instruction = instruction;
			if (eventType != null)
				if (eventType == "Update")
					stormPredictionCenterWarning.eventType = WarningEventType.Update;
				else if (eventType == "Alert")
					stormPredictionCenterWarning.eventType = WarningEventType.NewIssue;
				else if (eventType == "Cancel")
					stormPredictionCenterWarning.eventType = WarningEventType.Cancel;
				else if (eventType == "Ack")
					stormPredictionCenterWarning.eventType = WarningEventType.Acknowledge;
				else if (eventType == "Error")
					stormPredictionCenterWarning.eventType = WarningEventType.Error;

			dynamic parameters = feature.Properties["parameters"];
			if (parameters != null)
			{
				string cmamLongText = null;
				string cmamText = null;
				double maxWindGust = -1;
				double maxHailSize = -1;
				foreach (object o in parameters)
				{
					JProperty property = o as JProperty;
					if (property.Name == "NWSheadline")
						stormPredictionCenterWarning.NWSHeadline = (string)property.Value[0];
					else if (property.Name == "windThreat")
						stormPredictionCenterWarning.windThreat = (string)property.Value[0];
					else if (property.Name == "maxWindGust")
					{
						string winds = Regex.Matches((string)property.Value[0], @"\d+").First().Value;
						stormPredictionCenterWarning.maxWindGust = double.Parse(winds);
						string[] words = ((string)property.Value[0]).Split(' ');
						string unit = string.Empty;
						foreach (string word in words)
							if (word.Equals("mph", StringComparison.CurrentCultureIgnoreCase) || word.Equals("kts", StringComparison.CurrentCultureIgnoreCase))
								unit = word;
						stormPredictionCenterWarning.maxWindGustUnits = unit;
						maxWindGust = double.Parse(winds);
					}
					else if (property.Name == "hailThreat")
						stormPredictionCenterWarning.hailThreat = (string)property.Value[0];
					else if (property.Name == "maxHailSize")
					{
						string[] split = ((string)property.Value[0]).Split(' ');
						double hail = -1;
						foreach (string word in split)
							if (double.TryParse(word, out double result))
								hail = result;
						stormPredictionCenterWarning.maxHailSize = hail;
						maxHailSize = hail;
					}
					else if (property.Name == "tornadoDetection")
						stormPredictionCenterWarning.tornadoDetection = (string)property.Value[0];
					else if (property.Name == "waterspoutDetection")
						stormPredictionCenterWarning.waterspoutDetection = (string)property.Value[0];
					else if (property.Name == "tornadoDamageThreat")
						stormPredictionCenterWarning.tornadoDamageThreat = (string)property.Value[0];
					else if (property.Name == "thunderstormDamageThreat")
						stormPredictionCenterWarning.thunderstormDamageThreat = (string)property.Value[0];
					else if (property.Name == "flashFloodDetection")
						stormPredictionCenterWarning.flashFloodDetection = (string)property.Value[0];
					else if (property.Name == "flashFloodDamageThreat")
						stormPredictionCenterWarning.flashFloodDamageThreat = (string)property.Value[0];
					else if (property.Name == "CMAMtext")
					{
						stormPredictionCenterWarning.cmamText = (string)property.Value[0];
						cmamText = (string)property.Value[0];
					}
					else if (property.Name == "CMAMlongtext")
					{
						stormPredictionCenterWarning.cmamLongText = (string)property.Value[0];
						cmamLongText = (string)property.Value[0];
					}
				}
				if (includeCustomWarnings)
					if (alert == "Tornado Warning" && description.Contains("tornado emergency", StringComparison.OrdinalIgnoreCase))
						stormPredictionCenterWarning.warningName = "Tornado Emergency";
					else if (alert == "Tornado Warning" && description.Contains("particularly dangerous situation", StringComparison.OrdinalIgnoreCase))
						stormPredictionCenterWarning.warningName = "PDS Tornado Warning";
					else if (alert == "Severe Thunderstorm Warning")
					{
						double stormSpeedMph = double.Parse(((string)parameters["eventMotionDescription"]).Split("...")[3].Replace("KT", string.Empty)) * 1.151;
						if (cmamLongText != null && cmamLongText.Contains("along a line", StringComparison.OrdinalIgnoreCase) && maxWindGust >= 70 && maxHailSize < 1.25 && stormSpeedMph >= 50 && instruction.Contains("widespread wind damage", StringComparison.OrdinalIgnoreCase))
							stormPredictionCenterWarning.warningName = "Derecho Warning";
					}
			}

			stormPredictionCenterWarning.warningName = alert;
			stormPredictionCenterWarnings.Add(stormPredictionCenterWarning);
		}

		return [..stormPredictionCenterWarnings];
	}
}
