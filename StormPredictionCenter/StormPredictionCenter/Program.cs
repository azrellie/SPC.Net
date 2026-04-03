using Azrellie.Meteorology.SPC;

/*
added:
added points class (that can also get forecasts)
added a new type to mesosale discussions (outlook upgrade)
*/
namespace SPC_Testing
{
	internal class Program
	{
		readonly static StormPredictionCenter spc = new();

		static async Task Main()
		{
			spc.enableDebugLogging = true;
			spc.events.enableEvents();
			spc.events.watchIssued += Events_watchIssued;
			spc.events.mesoscaleDiscussionIssued += Events_mesoscaleDiscussionIssued;
			spc.events.convectiveWarningIssued += Events_warningIssued;

			while (true)
			{
				string? line = Console.ReadLine();
				if (line == "exit")
					break;
				if (line == "show mds")
					Console.WriteLine(string.Join(", ", spc.events.lastMds));
				else if (line == "show watches")
					Console.WriteLine(string.Join(", ", spc.events.lastWatches));
				else if (line == "show warns")
					Console.WriteLine(string.Join(", ", spc.events.lastWarnings));
			}
		}

		private static void Events_warningIssued(object sender, StormPredictionCenterWarning warning, WarningEventType eventType)
		{
			if (warning.warningName == "Tornado Warning")
				Console.ForegroundColor = ConsoleColor.Red;
			else if (warning.warningName == "Tornado Watch")
				Console.ForegroundColor = ConsoleColor.Yellow;
			else if (warning.warningName == "Severe Thunderstorm Warning")
				Console.ForegroundColor = ConsoleColor.DarkRed;
			else if (warning.warningName == "Severe Thunderstorm Watch")
				Console.ForegroundColor = ConsoleColor.Magenta;
			else
				Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine(warning.warningName + " - " + eventType);
			Console.WriteLine(warning.ToString() + " | https://api.weather.gov/alerts/" + warning.id);
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private static void Events_mesoscaleDiscussionIssued(object sender, StormPredictionCenterMesoscaleDiscussion[] mds)
		{
			Console.WriteLine(DateTime.Now);
			foreach (var md in mds)
				Console.WriteLine($"------------- {md.fullName} -------------\ntype: {md.type}\nissued: {md.issued}\nareas affected:\n{md.areasAffected}\n{md.url}\n------------------------------------------------------");
		}

		private static void Events_watchIssued(object sender, StormPredictionCenterWatch[] watches, StormPredictionCenterWatchBox[] watchBoxes)
		{
			Console.WriteLine("\n\n\n\n");
			Console.WriteLine(DateTime.Now);
			foreach (var watch in watches)
				Console.WriteLine($"------------- watch {watch.watchNumber} -------------\ntype: {watch.watchType}\nissued: {watch.sent}\nareas affected:\nhazards: {watch.watchHazards}\n{string.Join(", ", watch.counties)}\n\nheadline: {watch.headline}\ndesc:\n{watch.description}\n");
			Console.WriteLine("watch box data:");
			foreach (var watch in watchBoxes)
			{
				Console.WriteLine(watch.ToString());
			}
		}
	}
}