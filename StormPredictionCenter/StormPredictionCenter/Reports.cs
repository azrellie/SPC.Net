namespace Azrellie.Meteorology.SPC;

/// <summary>
/// Retrieve every storm report from the SPC.
/// </summary>
public class Reports(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	public async Task<SPCStormReport[]> getTodaysReports(ReportType reportType)
	{
		List<SPCStormReport> reports = [];
		if (reportType == ReportType.Tornado)
		{
			await Utils.downloadFileAsync("https://www.spc.noaa.gov/climo/reports/today_raw_torn.csv", Path.GetTempPath() + "\\spc temp\\tornado_reports_today.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\tornado_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = split[1];
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		else if (reportType == ReportType.Wind)
		{
			await Utils.downloadFileAsync("https://www.spc.noaa.gov/climo/reports/today_raw_hail.csv", Path.GetTempPath() + "\\spc temp\\wind_reports_today.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\wind_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = double.Parse(split[1]);
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		else if (reportType == ReportType.Hail)
		{
			await Utils.downloadFileAsync("https://www.spc.noaa.gov/climo/reports/today_raw_hail.csv", Path.GetTempPath() + "\\spc temp\\hail_reports_today.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\hail_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = double.Parse(split[1]) / 100;
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		return [..reports];
	}

	public async Task<SPCStormReport[]> getReportsAtDate(ReportType reportType, int year, int month, int day)
	{
		List<SPCStormReport> reports = [];
		if (reportType == ReportType.Tornado)
		{
			await Utils.downloadFileAsync($"https://www.spc.noaa.gov/climo/reports/{year:D2}{month:D2}{day:D2}_rpts_raw_torn.html", Path.GetTempPath() + $"\\spc temp\\tornado_reports_{year}{month:D2}{day:D2}.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\tornado_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = split[1];
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		else if (reportType == ReportType.Wind)
		{
			await Utils.downloadFileAsync($"https://www.spc.noaa.gov/climo/reports/{year:D2}{month:D2}{day:D2}_rpts_raw_wind.html", Path.GetTempPath() + $"\\spc temp\\wind_reports_{year}{month:D2}{day:D2}.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\wind_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = double.Parse(split[1]);
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		else if (reportType == ReportType.Hail)
		{
			await Utils.downloadFileAsync($"https://www.spc.noaa.gov/climo/reports/{year:D2}{month:D2}{day:D2}_rpts_raw_hail.html", Path.GetTempPath() + $"\\spc temp\\hail_reports_{year}{month:D2}{day:D2}.csv");
			string[] lines = File.ReadAllLines(Path.GetTempPath() + "\\spc temp\\hail_reports_today.csv");
			for (int i = 0; i < lines.Length; i++)
				if (i > 1)
				{
					string line = lines[i];
					if (line.Length == 0) continue;
					string[] split = line.Split(',');
					SPCStormReport report = new();
					int hour = int.Parse(split[0][..1]);
					int min = int.Parse(split[0][2..3]);
					report.time = DateTime.SpecifyKind(DateTime.Today.Add(new(hour, min, 0)), DateTimeKind.Utc);
					report.magnitude = double.Parse(split[1]) / 100;
					report.location = split[2];
					report.county = split[3];
					report.county = split[4];
					report.latitude = double.Parse(split[5]);
					report.longitude = double.Parse(split[6]);
					report.remarks = split[7];
				}
		}
		return [..reports];
	}
}