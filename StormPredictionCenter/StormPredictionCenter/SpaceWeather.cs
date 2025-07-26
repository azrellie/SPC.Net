using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Azrellie.Meteorology.SPC;

public class SpaceWeather(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	/// <summary>
	/// Gets the current aurora forecast from the Space Weather Prediction Center.
	/// </summary>
	/// <returns>An <see cref="SWPCAurora"/> object containing geographic data of the aurora.</returns>
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

	/// <summary>
	/// Gets the current solar wind speed.
	/// </summary>
	/// <param name="getAll">Whether or not to include all observations of the solar wind.</param>
	/// <returns>An array containing <see cref="SWPCSolarWind"/> objects.</returns>
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
		var swpcSolarWindSorted = swpcSolarWind.OrderBy(obj => obj.TimeOfObservation);
		if (!getAll)
		{
			SWPCSolarWind wind = swpcSolarWindSorted.Last();
			swpcSolarWind.Clear();
			swpcSolarWind.Add(wind);
		}
		else
			foreach (var entry in swpcSolarWindSorted)
				swpcSolarWind.Add(entry);
		return [..swpcSolarWind];
	}

	// TODO: work on
	public async Task<SWPCSolarWind[]> get10_7cmRadioFlux(bool getAll = false)
	{
		List<SWPCSolarWind> swpcSolarWind = [];
		JArray data = JArray.Parse(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/json/f107_cm_flux.json"));
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
		var swpcSolarWindSorted = swpcSolarWind.OrderBy(obj => obj.TimeOfObservation);
		if (!getAll)
		{
			SWPCSolarWind wind = swpcSolarWindSorted.Last();
			swpcSolarWind.Clear();
			swpcSolarWind.Add(wind);
		}
		else
			foreach (var entry in swpcSolarWindSorted)
				swpcSolarWind.Add(entry);
		return [..swpcSolarWind];
	}

	/// <summary>
	/// Gets the current Kp index.
	/// </summary>
	/// <param name="getAll">Whether or not to include all observations of the Kp index.</param>
	/// <returns>An array containing <see cref="SWPCKIndex"/> objects.</returns>
	public async Task<SWPCKIndex[]> getKIndex(bool getAll = false)
	{
		List<SWPCKIndex> swpcKIndex = [];
		JArray data = JArray.Parse(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/json/planetary_k_index_1m.json"));
		int i = 0;
		foreach (JObject d in data)
		{
			if (i > 1)
			{
				DateTime time = DateTime.Parse((string)d["time_tag"]);
				double kpIndex = (double)d["kp_index"];
				double estimatedKp = (double)d["estimated_kp"];
				string kp = (string)d["kp"];
				swpcKIndex.Add(new(time, kpIndex, estimatedKp, kp));
			}
			i++;
		}
		var swpcKIndexSorted = swpcKIndex.OrderBy(obj => obj.TimeOfObservation);
		if (!getAll)
		{
			SWPCKIndex wind = swpcKIndexSorted.Last();
			swpcKIndex.Clear();
			swpcKIndex.Add(wind);
		}
		else
			foreach (var entry in swpcKIndexSorted)
				swpcKIndex.Add(entry);
		return [..swpcKIndex];
	}

	/// <summary>
	/// Gets the current geomagnetic storm intensity based on the Kp index (thresholds per defined by NOAA at <see href="https://www.swpc.noaa.gov/noaa-scales-explanation"/>).
	/// </summary>
	/// <returns>The "G" classification of the current geomagnetic storm.</returns>
	public async Task<string> getCurrentGeomagneticStormIntensity()
	{
		SWPCKIndex kIndex = (await getKIndex())[0];
		string geoStorm = "Very Quiet";
		if (kIndex.KPIndex >= 2 && kIndex.KPIndex < 4)
			geoStorm = "Quiet";
		else if (kIndex.KPIndex >= 4 && kIndex.KPIndex < 5)
			geoStorm = "Active";
		else if (kIndex.KPIndex >= 5 && kIndex.KPIndex < 6)
			geoStorm = "G1";
		else if (kIndex.KPIndex >= 6 && kIndex.KPIndex < 7)
			geoStorm = "G2";
		else if (kIndex.KPIndex >= 7 && kIndex.KPIndex < 8)
			geoStorm = "G3";
		else if (kIndex.KPIndex >= 8 && kIndex.KPIndex < 9)
			geoStorm = "G4";
		else if (kIndex.KPIndex >= 9)
			geoStorm = "G5";
		return geoStorm;
	}

	/// <summary>
	/// Gets the current solar radiation storm intensity.
	/// </summary>
	/// <returns>A tuple object containing the <see cref="SWPCSolarRadiationStorm"/> object and the "S" classification of the storm as a <see cref="string"/></returns>
	public async Task<(SWPCSolarRadiationStorm, string)> getCurrentSolarRadiationStormIntensity()
	{
		List<SWPCSolarRadiationStorm> swpcSolarRadiationStorm = [];
		JArray data = JArray.Parse(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/json/goes/primary/integral-protons-6-hour.json"));
		DateTime now = DateTime.UtcNow;
		foreach (JObject d in data)
		{
			DateTime time = DateTime.Parse((string)d["time_tag"]);
			TimeSpan timeSpan = now - time;
			if (timeSpan.TotalMinutes > 14) continue; // ignore data older than 14 minutes (extra minutes to compensate for delays in latest data availability)
			double flux = (double)d["flux"];
			string energy = (string)d["energy"];
			swpcSolarRadiationStorm.Add(new(time, flux, energy));
		}
		var swpcSolarRadiationStormSorted = swpcSolarRadiationStorm.OrderBy(obj => obj.TimeOfObservation);
		SWPCSolarRadiationStorm solarRadiationStormData = null;
		foreach (SWPCSolarRadiationStorm storm in swpcSolarRadiationStorm)
			if (storm.Energy == ">=10 MeV")
				solarRadiationStormData = storm;
		string intensity = "None";
		if (solarRadiationStormData.ProtonFlux >= 10 && solarRadiationStormData.ProtonFlux < 100)
			intensity = "S1";
		else if (solarRadiationStormData.ProtonFlux >= 100 && solarRadiationStormData.ProtonFlux < 1000)
			intensity = "S2";
		else if (solarRadiationStormData.ProtonFlux >= 1000 && solarRadiationStormData.ProtonFlux < 10000)
			intensity = "S3";
		else if (solarRadiationStormData.ProtonFlux >= 10000 && solarRadiationStormData.ProtonFlux < 100000)
			intensity = "S4";
		else if (solarRadiationStormData.ProtonFlux >= 100000)
			intensity = "S5";
		return (solarRadiationStormData, intensity);
	}

	// TODO: work on
	/// <summary>
	/// Gets the current radio blackout intensity.
	/// </summary>
	/// <returns>A tuple object containing the <see cref="SWPCSolarRadiationStorm"/> object and the "R" classification of the storm as a <see cref="string"/></returns>
	public async Task<(SWPCSolarRadiationStorm, string)> getCurrentRadioBlackoutIntensity()
	{
		List<SWPCSolarRadiationStorm> swpcSolarRadiationStormPoint = [];
		JArray data = JArray.Parse(await Utils.downloadStringAsync("https://services.swpc.noaa.gov/json/goes/primary/integral-protons-6-hour.json"));
		foreach (JObject d in data)
		{
			DateTime time = DateTime.Parse((string)d["time_tag"]);
			double flux = (double)d["flux"];
			string energy = (string)d["energy"];
			swpcSolarRadiationStormPoint.Add(new(time, flux, energy));
		}
		var swpcSolarRadiationStormSorted = swpcSolarRadiationStormPoint.OrderBy(obj => obj.TimeOfObservation);
		SWPCSolarRadiationStorm solarRadiationStormData = swpcSolarRadiationStormSorted.Last();
		string intensity = "None";
		if (solarRadiationStormData.ProtonFlux >= 10 && solarRadiationStormData.ProtonFlux < 100)
			intensity = "S1";
		else if (solarRadiationStormData.ProtonFlux >= 100 && solarRadiationStormData.ProtonFlux < 1000)
			intensity = "S2";
		else if (solarRadiationStormData.ProtonFlux >= 1000 && solarRadiationStormData.ProtonFlux < 10000)
			intensity = "S3";
		else if (solarRadiationStormData.ProtonFlux >= 10000 && solarRadiationStormData.ProtonFlux < 100000)
			intensity = "S4";
		else if (solarRadiationStormData.ProtonFlux >= 100000)
			intensity = "S5";
		return (solarRadiationStormData, intensity);
	}
}