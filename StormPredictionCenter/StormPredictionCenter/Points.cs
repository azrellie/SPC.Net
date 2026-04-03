using Newtonsoft.Json;

namespace Azrellie.Meteorology.SPC;

public class Points(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	public async Task<PointRef> getPoint(float lat, float lng) => JsonConvert.DeserializeObject<PointRef>(await Utils.downloadStringAsync($"https://api.weather.gov/points/{lat},{lng}"))!;

	public async Task<string> getRadioFromPoint(float lat, float lng) => await Utils.downloadStringAsync($"https://api.weather.gov/points/{lat},{lng}/radio");

	public async Task<Forecast> getForecast(float lat, float lng) => JsonConvert.DeserializeObject<Forecast>(await Utils.downloadStringAsync((await getPoint(lat, lng)).properties.forecast))!;

	public async Task<Forecast> getForecast(int x, int y, string forecastOffice) => JsonConvert.DeserializeObject<Forecast>(await Utils.downloadStringAsync($"https://api.weather.gov/gridpoints/{forecastOffice}/{x},{y}/forecast"))!;

	public async Task<Forecast> getHourlyForecast(float lat, float lng) => JsonConvert.DeserializeObject<Forecast>(await Utils.downloadStringAsync((await getPoint(lat, lng)).properties.forecastHourly))!;

	public async Task<Forecast> getHourlyForecast(int x, int y, string forecastOffice) => JsonConvert.DeserializeObject<Forecast>(await Utils.downloadStringAsync($"https://api.weather.gov/gridpoints/{forecastOffice}/{x},{y}/forecast/hourly"))!;

	public async Task<ForecastOffice> getForecastOffice(float lat, float lng) => JsonConvert.DeserializeObject<ForecastOffice>(await Utils.downloadStringAsync((await getPoint(lat, lng)).properties.forecastOffice))!;
}