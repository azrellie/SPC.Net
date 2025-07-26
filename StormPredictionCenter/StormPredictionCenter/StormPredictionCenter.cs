using HtmlAgilityPack;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

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

namespace Azrellie.Meteorology.SPC;

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

	/// <summary>
	/// Enables debug logging.
	/// </summary>
	public bool enableDebugLogging = true;

	public Outlooks outlooks;
	public Reports reports;
	public Watches watches;
	public Warnings warnings;
	public Archive archive;
	public NHC nhc;
	public Radar radar;
	public Events events;
	public SpaceWeather spaceWeather;

	internal void debugLog(dynamic message)
	{
		if (enableDebugLogging)
			Console.WriteLine("[SPC] " + message);
	}

	public StormPredictionCenter()
	{
		outlooks = new(this);
		reports = new(this);
		watches = new(this);
		warnings = new(this);
		archive = new(this);
		nhc = new(this);
		radar = new(this);
		events = new(this);
		spaceWeather = new(this);
	}
}

/// <summary>
/// Util methods for this class. These methods are not intended to be accessed outside this script, but they are public anyway. (may change in future versions)
/// </summary>
public class Utils
{
	private static HttpClient http = new()
	{
		Timeout = TimeSpan.FromSeconds(60)
	};

	static Utils()
	{
		http.DefaultRequestHeaders.UserAgent.ParseAdd("C# Code");
	}

	// watch numbers can be obtained through the vtec property and might be better to use that over this current method
	public static int getSevereThunderstormWatchNumber(string text)
	{
		string[] split = text.Replace('\n', ' ').Split(' ');
		string matchingWord = string.Empty;
		foreach (string word in split)
		{
			string lower = word.ToLower();

			// we found a match, that means the next word will be the watch number
			if (matchingWord == "severe thunderstorm watch " || matchingWord == "severe thunderstorm\nwatch " || matchingWord == "severe thunderstorm watch\n" || matchingWord == "severe\nthunderstorm watch ")
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
			if (matchingWord == "tornado watch " || matchingWord == "tornado watch\n" || matchingWord == "tornado\nwatch ")
				return int.Parse(word.Where(char.IsDigit).ToArray());

			// check if "lower" matches to any of these words, if it does, concat it to the matchingWord string variable
			if (lower == "tornado" || lower == "watch")
				matchingWord += lower + " ";
		}
		return 0;
	}

	public static async Task<MemoryStream?> processKmz(string url)
	{
		try
		{
			string fileName = Path.GetFileName(url);
			MemoryStream? kmzStream = await downloadFileAsStreamAsync(url);
			if (kmzStream == null) return null;
			kmzStream.Position = 0;
			ZipArchive zipArchive = new(kmzStream, ZipArchiveMode.Read, true);

			var entry = zipArchive.GetEntry(Path.ChangeExtension(fileName, "kml"));
			MemoryStream kmlStream = new();
			using Stream entryStream = entry.Open();
			entryStream.CopyTo(kmlStream);
			kmlStream.Position = 0;
			return kmlStream;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[SPC] Error processing kmz: " + ex);
		}
		return null;
	}

	public static async Task waitDelete(string file)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (true)
		{
			try
			{
				File.Delete(file);
				break;
			}
			catch
			{
				if (stopwatch.ElapsedMilliseconds > 2000) break;
			}
			await Task.Delay(100);
		}
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
		try
		{
			return http.GetStringAsync(url).Result;
		}
		catch (Exception ex)
		{
			if (!ex.ToString().Contains("504 (Gateway Time-out)"))
				Console.WriteLine(ex.ToString() + " | " + url);
			return string.Empty;
		}
	}

	public static async Task<string> downloadStringAsync(string url)
	{
		try
		{
			return await http.GetStringAsync(url);
		}
		catch (Exception ex)
		{
			if (!ex.ToString().Contains("504 (Gateway Time-out)"))
				Console.WriteLine(ex.ToString() + " | " + url);
			return string.Empty;
		}
	}

	public static async void downloadFile(string url, string fileName)
	{
		if (File.Exists(fileName))
			File.Delete(fileName);
		Stream stream = await http.GetStreamAsync(url);
		using FileStream fs = File.OpenWrite(fileName);
		stream.CopyTo(fs);
		fs.Close();
		stream.Close();
	}

	public static async Task<bool> downloadFileAsync(string url, string fileName)
	{
		try
		{
			if (File.Exists(fileName))
				File.Delete(fileName);

			using var response = await http.GetAsync(url);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync();
			await using var fs = File.Create(fileName);
			await stream.CopyToAsync(fs);
			stream.Close();

			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Download failed: {ex.Message}");
			return false;
		}
	}

	public static async Task<MemoryStream?> downloadFileAsStreamAsync(string url)
	{
		try
		{
			using var response = await http.GetAsync(url);
			response.EnsureSuccessStatusCode();
			Stream stream = await response.Content.ReadAsStreamAsync();
			MemoryStream memStream = new();
			stream.CopyTo(memStream);
			return memStream;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Download failed: {ex.Message}");
			return null;
		}
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
			Console.WriteLine($"[SPC] Error fetching or parsing the webpage: {ex.Message}");
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

	public static float degToRad(float deg) => deg * (float)Math.PI / 180f;

	public static float roundToSpecifiedValues(float number, List<float> values)
	{
		float minDiff = float.MaxValue;
		float roundedNum = 0;

		foreach (var value in values)
		{
			float diff = Math.Abs(number - value);
			if (diff < minDiff)
			{
				minDiff = diff;
				roundedNum = value;
			}
		}

		return roundedNum;
	}
}