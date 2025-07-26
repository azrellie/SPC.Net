using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Azrellie.Meteorology.SPC;

public class SpaceWeather(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	public async Task<SWPCAurora> getAuroraForecast()
	{
		SWPCAurora swpcAurora = new();
		JObject? data = JsonConvert.DeserializeObject<JObject>(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/json/ovation_aurora_latest.json"));
		swpcAurora.ObservationTime = DateTime.Parse((string)data["Observation Time"]);
		swpcAurora.ForecastTime = DateTime.Parse((string)data["Forecast Time"]);
		foreach (JArray d in data["coordinates"])
		{
			int aurora = (int)d[0];
			int lng = (int)d[1];
			int lat = (int)d[2];
			swpcAurora.Data.Add(new(lng, lat, aurora));
		}
		return swpcAurora;
	}

	public async Task<SWPCSolarWind[]> getSolarWind(bool getAll = false)
	{
		List<SWPCSolarWind> swpcSolarWind = [];
		JArray data = JArray.Parse(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/products/geospace/propagated-solar-wind-1-hour.json"));
		int i = 0;
		foreach (JArray d in data)
		{
			if (i > 1)
			{
				DateTime time = DateTime.Parse((string)d[0]);
				double speed = (double)d[1];
				double density = (double)d[2];
				double temp = (double)d[3];
				swpcSolarWind.Add(new(time, speed, density, temp));
			}
			i++;
		}
		swpcSolarWind.OrderBy(obj => obj.TimeOfObservation);
		if (!getAll)
		{
			SWPCSolarWind wind = swpcSolarWind.Last();
			swpcSolarWind.Clear();
			swpcSolarWind.Add(wind);
		}
		return [..swpcSolarWind];
	}
}