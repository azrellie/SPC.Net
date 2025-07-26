using Azrellie.Misc.ExtendedTimer;

namespace Azrellie.Meteorology.SPC;

// TODO: add events for newly issued mesoscale discussions, warnings, and tropical cyclones
public class Events
{
	private StormPredictionCenter? parent;

	public delegate void WatchIssuedEventHandler(object sender, StormPredictionCenterWatch[] watches, StormPredictionCenterWatchBox[] watchBoxes);
	public delegate void MesoscaleDiscussionEventHandler(object sender, StormPredictionCenterMesoscaleDiscussion[] mds);
	public delegate void WarningIssueEventHandler(object sender, StormPredictionCenterWarning warning, WarningEventType eventType);

	/// <summary>
	/// Fired whenever the Storm Prediction Center issues a tornado/severe thunderstorm watch.
	/// </summary>
	public event WatchIssuedEventHandler watchIssued;

	/// <summary>
	/// Fired whenever the Storm Prediction Center issues a mesoscale discussion.
	/// </summary>
	public event MesoscaleDiscussionEventHandler mesoscaleDiscussionIssued;

	/// <summary>
	/// Fired whenever the National Weather Service issues a new warning. Only used for convective and some hydrologic warnings.
	/// </summary>
	public event WarningIssueEventHandler warningIssued;

	/// <summary>
	/// Allows events to be enabled.
	/// </summary>
	public void enableEvents()
	{
		timer.Resume();
		eventsEnabled = true;
		parent?.debugLog("Events have been enabled.");
	}

	/// <summary>
	/// Allows events to be disabled.
	/// </summary>
	public void disableEvents()
	{
		timer.Pause();
		eventsEnabled = false;
		parent?.debugLog("Events have been disabled.");
	}

	private bool eventsEnabled = false;
	private readonly ExtendedTimer timer = new();
	private readonly Warnings warnings = new(null);
	private readonly Watches watches = new(null);
	private readonly Outlooks outlooks = new(null);
	public readonly HashSet<int> lastWatches = []; // use hashsets over lists to prevent duplicates and faster lookups
	public readonly HashSet<int> lastWatchBoxes = [];
	public readonly HashSet<int> lastMds = [];
	public readonly HashSet<string> lastWarnings = [];

	private async Task listenForConvectiveWatches()
	{
		StormPredictionCenterWatch[] tornadoWatches = await watches.getActiveTornadoWatches();
		StormPredictionCenterWatch[] severeThunderstormWatches = await watches.getActiveSevereThunderstormWatches();
		StormPredictionCenterWatchBox[] watchBoxes = await watches.getActiveWatchBoxes();

		DateTime timerStart = new(timer.TimeSinceStart, DateTimeKind.Utc);
		List<StormPredictionCenterWatch> theNewTorWatch = [];
		foreach (StormPredictionCenterWatch watch in tornadoWatches)
		{
			if (!lastWatches.Contains(watch.watchNumber) && timerStart < watch.sent.UtcDateTime && watch.status == WarningEventType.NewIssue)
			{
				parent?.debugLog("New tornado watch: " + watch.watchNumber);
				theNewTorWatch.Add(watch);
			}
			if (watch.status != WarningEventType.NewIssue)
				lastWatches.Add(watch.watchNumber);
		}

		List<StormPredictionCenterWatch> theNewSvrWatch = [];
		foreach (StormPredictionCenterWatch watch in severeThunderstormWatches)
		{
			if (!lastWatches.Contains(watch.watchNumber) && timerStart < watch.sent.UtcDateTime && watch.status == WarningEventType.NewIssue)
			{
				parent?.debugLog("New severe thunderstorm watch: " + watch.watchNumber);
				theNewSvrWatch.Add(watch);
			}
			if (watch.status != WarningEventType.NewIssue)
				lastWatches.Add(watch.watchNumber);
		}

		List<StormPredictionCenterWatchBox>? theNewWatchBox = [];
		foreach (StormPredictionCenterWatchBox watchBox in watchBoxes)
			if (!lastWatchBoxes.Contains(watchBox.watchNumber) && timerStart < watchBox.issued.UtcDateTime)
			{
				lastWatchBoxes.Add(watchBox.watchNumber);
				theNewWatchBox.Add(watchBox);
			}

		// refire check: dont call this event again if the found watches match the last ones stored
		foreach (StormPredictionCenterWatch watch in tornadoWatches)
		{
			if (timerStart < watch.sent) // watch has been issued after we started listening for events
				lastWatches.Add(watch.watchNumber);
			if (Math.Abs(watch.sent.UtcDateTime.Day - DateTimeOffset.UtcNow.Day) >= 1) // remove if the watch is a day old
				lastWatches.Remove(watch.watchNumber);
		}

		foreach (StormPredictionCenterWatch watch in severeThunderstormWatches)
		{
			if (timerStart < watch.sent) // watch has been issued after we started listening for events
				lastWatches.Add(watch.watchNumber);
			if (Math.Abs(watch.sent.UtcDateTime.Day - DateTimeOffset.UtcNow.Day) >= 1) // remove if the watch is a day old
				lastWatches.Remove(watch.watchNumber);
		}

		if (theNewSvrWatch.Count > 0)
			watchIssued?.Invoke(this, [..theNewSvrWatch], [..theNewWatchBox]);
		if (theNewTorWatch.Count > 0)
			watchIssued?.Invoke(this, [..theNewTorWatch], [..theNewWatchBox]);
	}

	private async Task listenForMesoscaleDiscussions()
	{
		StormPredictionCenterMesoscaleDiscussion[] mds = await outlooks.getLatestMesoscaleDiscussion();

		List<StormPredictionCenterMesoscaleDiscussion> newMds = [];
		foreach (StormPredictionCenterMesoscaleDiscussion md in mds)
			if (!lastMds.Contains(md.mesoscaleNumber) && new DateTime(timer.TimeSinceStart, DateTimeKind.Utc) < md.issued.UtcDateTime)
			{
				newMds.Add(md);
				lastMds.Add(md.mesoscaleNumber);
			}

		// refire check: dont call this event again if the found mesoscale discussions match the last ones stored
		foreach (StormPredictionCenterMesoscaleDiscussion md in mds)
			if (new DateTime(timer.TimeSinceStart, DateTimeKind.Utc) < md.issued) // md has been issued after we started listening for events
				lastMds.Add(md.mesoscaleNumber);

		if (newMds.Count > 0)
			mesoscaleDiscussionIssued?.Invoke(this, [..newMds]);
	}

	private void listenForWarnings()
	{
		foreach (var warning in warnings.getLatestWarnings(["tornado warning", "severe thunderstorm warning", "tornado watch", "severe thunderstorm watch", "special weather statement", "severe weather statement", "special marine warning", "marine weather statement", "ice storm warning", "snow squall warning"]))
		{
			TimeSpan timeDiff = new DateTime(timer.TimeSinceStart, DateTimeKind.Utc) - warning.sent;
			if (timeDiff.TotalHours >= 6 && (warning.warningName == "Special Weather Statement" && warning.description.Contains("thunderstorm", StringComparison.InvariantCultureIgnoreCase))) // if the registered warning is older than 6 hours, remove it
			{
				//parent?.debugLog($"Remove {warning.warningName} from the list because its more than 6 hours old.");
				lastWarnings.Remove(warning.id);
			}

			if (new DateTime(timer.TimeSinceStart, DateTimeKind.Utc) >= warning.sent) continue; // dont fire if the warning was issued before we started listening for events
			if (lastWarnings.Contains(warning.id)) continue; // dont fire if the warning is already registered in the last warnings list

			if (!lastWarnings.Contains(warning.id))
				lastWarnings.Add(warning.id);

			WarningEventType eventType = warning.eventType;
			if (warning.description.Contains("below severe limits", StringComparison.OrdinalIgnoreCase) || warning.description.Contains("allowed to expire", StringComparison.OrdinalIgnoreCase))
				eventType = WarningEventType.Update;
			if (warning.description.Contains("canceled", StringComparison.OrdinalIgnoreCase) || warning.description.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
				eventType = WarningEventType.Cancel;

			parent?.debugLog($"New alert {warning.warningName} has been issued.");
			warningIssued?.Invoke(this, warning, eventType);
		}
	}

	public Events(StormPredictionCenter self)
	{
		parent = self;
		timer.TickInterval = 10000;
		timer.TickOnStart = true;
		timer.OnTimerTick += async (sender, e) =>
		{
			if (!eventsEnabled) return;
			if (watchIssued != null)
				await listenForConvectiveWatches();
			if (mesoscaleDiscussionIssued != null)
				listenForMesoscaleDiscussions();
			if (warningIssued != null)
				listenForWarnings();
		};
		timer.Start();
		parent?.debugLog("Events timer has started.");
	}
}