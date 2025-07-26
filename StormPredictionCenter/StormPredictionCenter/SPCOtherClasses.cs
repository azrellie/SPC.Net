using GeoJSON.Net.Geometry;
using System.Drawing;

namespace Azrellie.Meteorology.SPC;

public record RiskArea
{
	public object riskType = 0;
	public DateTime valid = DateTime.UnixEpoch;
	public DateTime expire = DateTime.UnixEpoch;
	public DateTime issue = DateTime.UnixEpoch;
	public string label = string.Empty;
	public string label2 = string.Empty;
	public Color stroke = Color.White;
	public Color fill = Color.White;
	public bool isSignificant = false;
	public List<SPCPolygon> polygons = [];
	public override string ToString() => $"{label2} | {label} | Expires: {expire}, Issued: {issue}, Valid: {valid}";
}

public record StormPredictionCenterMesoscaleDiscussion
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

public record SPCStormReport
{
	public DateTime time = DateTime.MinValue;
	public object magnitude = 0;
	public string location = string.Empty;
	public string county = string.Empty;
	public string state = string.Empty;
	public double latitude = 0;
	public double longitude = 0;
	public string remarks = string.Empty;
}

public record StormPredictionCenterWarning
{
	public DateTime sent = DateTime.UnixEpoch;
	public DateTime effective = DateTime.UnixEpoch;
	public DateTime onset = DateTime.UnixEpoch;
	public DateTime expires = DateTime.UnixEpoch;
	public DateTime ends = DateTime.UnixEpoch;
	public List<string> affectedCounties = [];
	public string warningName = string.Empty;
	public string description = string.Empty;
	public string instruction = string.Empty;
	public WarningEventType eventType = WarningEventType.NewIssue;
	public string sender = string.Empty;
	public string headline = string.Empty;
	public string NWSHeadline = string.Empty;
	public string windThreat = string.Empty;
	public double? maxWindGust;
	public string maxWindGustUnits = string.Empty;
	public string hailThreat = string.Empty;
	public double? maxHailSize;
	public string tornadoDetection = string.Empty;
	public string waterspoutDetection = string.Empty;
	public string tornadoDamageThreat = string.Empty;
	public string thunderstormDamageThreat = string.Empty;
	public string flashFloodDamageThreat = string.Empty;
	public string flashFloodDetection = string.Empty;
	public string cmamText = string.Empty;
	public string cmamLongText = string.Empty;
	public string id = string.Empty;
	public IGeometryObject polygon;
	public override string ToString()
	{
		string returnString = warningName + " | " + NWSHeadline;
		if (warningName == "Tornado Warning")
		{
			string damageThreat = string.Empty;
			if (tornadoDamageThreat != string.Empty)
				damageThreat = " | Tornado Damage Threat: " + damageThreat;
			returnString = $"{warningName}{damageThreat} | Tornado: {tornadoDetection} | Max Hail Size: {maxHailSize} in";
		}
		else if (warningName == "Tornado Watch")
			returnString = $"{warningName} | Part of Tornado Watch {Utils.getTornadoWatchNumber(description)}";
		else if (warningName == "Severe Thunderstorm Warning")
		{
			string damageThreat = string.Empty;
			if (tornadoDamageThreat != string.Empty)
				damageThreat = " | Thunderstorm Damage Threat: " + damageThreat;
			string tornadoDetection = string.Empty;
			if (tornadoDetection != string.Empty)
				tornadoDetection = " | Tornado: " + tornadoDetection;
			returnString = $"{warningName}{tornadoDetection}{damageThreat} | Max Wind Gust: {maxWindGust} {maxWindGustUnits} | Max Hail Size: {maxHailSize} in";
		}
		else if (warningName == "Severe Thunderstorm Watch")
			returnString = $"{warningName} | Part of Severe Thunderstorm Watch {Utils.getSevereThunderstormWatchNumber(description)}";
		else if (warningName == "Special Weather Statement")
			if (maxWindGust != null)
				returnString = $"{warningName} | Max Hail Size: {maxHailSize} in | Max Wind Gust: {maxWindGust} {maxWindGustUnits}";
			else
				returnString = warningName + " | " + NWSHeadline == string.Empty ? NWSHeadline : headline;
		return returnString;
	}
}