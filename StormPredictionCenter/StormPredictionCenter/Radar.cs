using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.Compression;
using Azrellie.Meteorology.NexradNet.Level3;
using SkiaSharp;

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

	private Dictionary<float, SKColor> colorTableFromProduct(Enums.MessageCode code)
	{
		switch (code)
		{
			case Enums.MessageCode.BaseReflectivityLongRange:
			case Enums.MessageCode.BaseReflectivityShortRange:
			case Enums.MessageCode.DigitalBaseReflectivity:
			case Enums.MessageCode.DigitalHybridScanReflectivity:
			case Enums.MessageCode.HighLayerCompositeReflectivity:
			case Enums.MessageCode.HybridScanReflectivity:
			case Enums.MessageCode.LegacyBaseReflectivityLongRange:
			case Enums.MessageCode.SuperResolutionDigitalBaseReflectivity:
				return new()
				{
					{1, new(0, 236, 236)},
					{2, new(0, 160, 246)},
					{3, new(0, 0, 246)},
					{4, new(0, 255, 0)},
					{5, new(0, 200, 0)},
					{6, new(0, 144, 0)},
					{7, new(255, 255, 0)},
					{8, new(231, 192, 0)},
					{9, new(255, 144, 0)},
					{10, new(255, 0, 0)},
					{11, new(214, 0, 0)},
					{12, new(192, 0, 0)},
					{13, new(255, 0, 255)},
					{14, new(153, 85, 201)},
					{15, new(255, 255, 255)}
				};
			case Enums.MessageCode.CompositeReflectivity1:
			case Enums.MessageCode.CompositeReflectivity2:
			case Enums.MessageCode.CompositeReflectivity3:
			case Enums.MessageCode.CompositeReflectivity4:
			case Enums.MessageCode.LayerCompositeReflectivity:
			case Enums.MessageCode.LegacyBaseReflectivity1:
			case Enums.MessageCode.LegacyBaseReflectivity2:
			case Enums.MessageCode.LegacyBaseReflectivity3:
			case Enums.MessageCode.LegacyBaseReflectivity4:
			case Enums.MessageCode.LegacyBaseReflectivity6:
			case Enums.MessageCode.LowLayerCompositeReflectivity:
			case Enums.MessageCode.MidlayerCompositeReflectivity:
			case Enums.MessageCode.UserSelectableLayerCompositeReflectivity:
				return [];
			default:
				return [];
		}
	}

	private void processPacket16(SKCanvas canvas, SKBitmap bmp, Level3 level3, SymbologyPacket16 p16, Dictionary<float, (SKColor, SKPath)> geometry)
	{
		var colorTable = colorTableFromProduct(level3.Header.MessageCode);
		float noDataValue = 0;
		float centerX = bmp.Width / 2f;
		float centerY = bmp.Height / 2f;
		float rotationOffset = 90;
		float radarScale = 1;
		foreach (Radial radial in p16.Radials)
			if (radial is Radial255 r255)
			{
				float angleIncrement = 1 / (p16.Radials.Count / 360f);
				float adjustedAngle = -MathF.Floor((-(MathF.Round(radial.StartAngle * 2, MidpointRounding.AwayFromZero) / 2) + rotationOffset) * 10) / 10;
				float adjustedAngleNext = -MathF.Floor((-(MathF.Round((radial.StartAngle + angleIncrement) * 2, MidpointRounding.AwayFromZero) / 2) + rotationOffset) * 10) / 10;
				float angle = Utils.degToRad(adjustedAngle);
				float angleNext = Utils.degToRad(adjustedAngleNext);

				for (int binIndex = 0; binIndex < r255.Bins.Length; binIndex++)
				{
					SKPath pixel = new();

					float bin = r255.Bins[binIndex];
					if (bin == noDataValue) continue;
					float nextBinIndex = binIndex + 1;

					float pixelXBottomRight = centerX + MathF.Cos(angle) * binIndex * radarScale;
					float pixelYBottomRight = centerY + MathF.Sin(angle) * binIndex * radarScale;

					float pixelXTopRight = centerX + MathF.Cos(angle) * nextBinIndex * radarScale;
					float pixelYTopRight = centerY + MathF.Sin(angle) * nextBinIndex * radarScale;

					float pixelXBottomLeft = centerX + MathF.Cos(angleNext) * binIndex * radarScale;
					float pixelYBottomLeft = centerY + MathF.Sin(angleNext) * binIndex * radarScale;

					float pixelXTopLeft = centerX + MathF.Cos(angleNext) * nextBinIndex * radarScale;
					float pixelYTopLeft = centerY + MathF.Sin(angleNext) * nextBinIndex * radarScale;

					pixel.MoveTo(pixelXBottomRight, pixelYBottomRight);
					pixel.LineTo(pixelXBottomLeft, pixelYBottomLeft);
					pixel.LineTo(pixelXTopLeft, pixelYTopLeft);
					pixel.LineTo(pixelXTopRight, pixelYTopRight);

					SKColor color;
					if (level3.Header.MessageCode == Enums.MessageCode.SuperResolutionDigitalBaseReflectivity ||
						level3.Header.MessageCode == Enums.MessageCode.BaseReflectivityLongRange ||
						level3.Header.MessageCode == Enums.MessageCode.BaseReflectivityShortRange ||
						level3.Header.MessageCode == Enums.MessageCode.DigitalBaseReflectivity ||
						level3.Header.MessageCode == Enums.MessageCode.DigitalHybridScanReflectivity ||
						level3.Header.MessageCode == Enums.MessageCode.HybridScanReflectivity ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivity1 ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivity2 ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivity3 ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivity4 ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivity6 ||
						level3.Header.MessageCode == Enums.MessageCode.LegacyBaseReflectivityLongRange)
						color = colorTable[Utils.roundToSpecifiedValues(bin / 16.8f, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15])];
					else
						color = colorTable[bin];
					if (!geometry.TryAdd(bin, (color, pixel)))
						geometry[bin].Item2.AddPath(pixel);
				}
			}
	}

	public string radarDataToImage(string file, int width, int height)
	{
		/*Dictionary<float, (SKColor, SKPath)> geometry = [];
		SKBitmap bmp = new(width, height);
		SKCanvas canvas = new(bmp);
		canvas.Clear(SKColors.Transparent);
		using FileStream fs = File.OpenRead(file);
		BinaryReader reader = new(fs);
		Level3 level3 = new(ref reader);
		foreach (List<SymbologyPacket> packets in level3.ProductSymbology.SymbologyPackets)
			foreach (SymbologyPacket packet in packets)
				if (packet is SymbologyPacket16 p16)
					processPacket16(canvas, bmp, level3, p16, geometry);
		foreach (var geo in geometry)
			canvas.DrawPath(geo.Value.Item2, new()
			{
				Color = geo.Value.Item1
			});
		SKData data = bmp.Encode(SKEncodedImageFormat.Png, 100);
		string fileName = $"{level3.TextHeader.RadarStationId}_{level3.TextHeader.DataType}_{level3.ProductDescription.GenerationDateOfProduct}.png";
		using FileStream img = File.OpenWrite(fileName);
		data.SaveTo(img);
		return fileName;*/
		return string.Empty;
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