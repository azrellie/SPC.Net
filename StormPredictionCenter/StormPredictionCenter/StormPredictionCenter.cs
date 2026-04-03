using HtmlAgilityPack;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

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
	public Radio radio;
	public Points points;

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
		radio = new(this);
		points = new(this);
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
		}catch{}
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
		catch
		{
			return string.Empty;
		}
	}

	public static async Task<string> downloadStringAsync(string url)
	{
		try
		{
			return await http.GetStringAsync(url);
		}
		catch
		{
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
			if (!Directory.Exists(Path.GetDirectoryName(fileName)))
				Directory.CreateDirectory(Path.GetDirectoryName(fileName));

			using var response = await http.GetAsync(url);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync();
			await using var fs = File.Create(fileName);
			await stream.CopyToAsync(fs);
			stream.Close();

			return true;
		}
		catch
		{
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
		catch
		{
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
		}catch{}

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