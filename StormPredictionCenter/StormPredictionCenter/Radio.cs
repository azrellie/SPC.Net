namespace Azrellie.Meteorology.SPC;

public class Radio(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	public async Task<List<NWRBroadcast>> getNWRBroadcasts(string callsign = "none")
	{
		var data = await Utils.downloadStringAsync("https://www.weather.gov/source/nwr/JS/CCL.js");
		data = data.Replace(";", string.Empty).Replace("\"", string.Empty);
		string[] lines = [..data.Split('\n').Skip(16)];
		List<NWRBroadcast> broadcasts = [];
		NWRBroadcast broadcast = new();
		foreach (var line in lines)
		{
			if (line == "\n") continue;
			string[] splitLine = line.Split(" = ");
			if (line.StartsWith("ST["))
				broadcast.StateAbbreviation = splitLine[1];
			else if (line.StartsWith("STATE["))
				broadcast.State = splitLine[1];
			else if (line.StartsWith("COUNTY["))
				broadcast.County = splitLine[1];
			else if (line.StartsWith("SAME["))
				broadcast.SAME = splitLine[1];
			else if (line.StartsWith("SITENAME["))
				broadcast.SiteName = splitLine[1];
			else if (line.StartsWith("SITELOC["))
				broadcast.SiteLocation = splitLine[1];
			else if (line.StartsWith("SITESTATE["))
				broadcast.SiteState = splitLine[1];
			else if (line.StartsWith("FREQ["))
			{
				if (splitLine[1] == string.Empty) continue;
				broadcast.Frequency = float.Parse(splitLine[1]);
			}
			else if (line.StartsWith("CALLSIGN["))
				broadcast.Callsign = splitLine[1];
			else if (line.StartsWith("LAT["))
			{
				if (splitLine[1] == string.Empty) continue;
				broadcast.Latitude = float.Parse(splitLine[1]);
			}
			else if (line.StartsWith("LON["))
			{
				if (splitLine[1] == string.Empty) continue;
				broadcast.Longitude = float.Parse(splitLine[1]);
			}
			else if (line.StartsWith("PWR["))
			{
				if (splitLine[1] == string.Empty) continue;
				broadcast.PowerOutput = int.Parse(splitLine[1]);
			}
			else if (line.StartsWith("STATUS["))
				broadcast.Status = splitLine[1];
			else if (line.StartsWith("WFO["))
				broadcast.WeatherForecastOffice = splitLine[1];
			else if (line.StartsWith("REMARKS["))
			{
				broadcast.Remarks = splitLine[1];
				if (broadcast.Callsign.Equals(callsign, StringComparison.CurrentCultureIgnoreCase)) // call sign specified. look for that call sign, add it here and break
				{
					broadcasts.Add(broadcast);
					break;
				}

				if (callsign == "none") // anyway because no call sign was specified
					broadcasts.Add(broadcast);

				broadcast = new();
			}
		}
		return broadcasts;
	}

	public async Task<string> getNOAAWeatherRadioBroadcast(string callsign) => await Utils.downloadStringAsync($"https://api.weather.gov/radio/{(await getNWRBroadcasts(callsign))[0].Callsign}/broadcast");
}