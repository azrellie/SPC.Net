using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.Compression;

namespace Azrellie.Meteorology.SPC;

/// <summary>
/// Gather radar stations and radar images (and soon raw radar data) from the NWS.
/// </summary>
public class Radar(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

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