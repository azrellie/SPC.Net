#pragma warning disable
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

namespace Azrellie.Misc.ExtendedTimer;

public enum TimerState
{
	Running,
	Paused,
	Stopped
}

public enum TickMethod
{
	/// <summary>
	/// Make the timer use <see cref="Task.Delay(int)"/> for ticking, but is highly prone to drift (millisecond to precision) resulting in unreliable delta times. Useful for servers or basic low frequency ticking.
	/// </summary>
	TaskDelay,
	/// <summary>
	/// Make the timer use <see cref="Stopwatch"/> for ticking. Highly precise with minimal drift (microsecond precision). Useful for engine/rendering ticks.
	/// </summary>
	StopwatchLoop
}

public class ExtendedTimer : IDisposable
{
	/// <summary>
	/// Creates a new instance of this class.
	/// </summary>
	/// <param name="startNow">Whether the timer should start immediately upon construction.</param>
	public ExtendedTimer(bool startNow = false)
	{
		if (startNow)
			Start();
	}

	// internal stuff
	private TickMethod tickMethod = TickMethod.TaskDelay;
	private bool paused = false;
	private Stopwatch sw = Stopwatch.StartNew();

	// properties
	/// <summary>
	/// Determines whether <see cref="OnTimerTick"/> will be fired immediately upon starting the timer. This is false by default.
	/// </summary>
	public bool TickOnStart { get; set; } = false;

	/// <summary>
	/// How many times this timers <see cref="OnTimerTick"/> has been fired.
	/// </summary>
	public long TickCount { get; private set; } = 0;

	/// <summary>
	/// How long to wait to start firing <see cref="OnTimerTick"/> when <see cref="Start"/> is called (in milliseconds).
	/// </summary>
	public long StartDelay { get; set; } = 0;

	/// <summary>
	/// The date time (with timezone info) since this <see cref="ExtendedTimer"/> has started.
	/// </summary>
	public DateTimeOffset TimeAtStart { get; private set; }

	/// <summary>
	/// Whether this <see cref="ExtendedTimer"/> is allowed to fire <see cref="OnTimerTick"/>. Default value is false.
	/// </summary>
	public bool Enabled { get; set; } = false;

	/// <summary>
	/// The amount (in milliseconds) of time to wait before firing <see cref="OnTimerTick"/>. Default value is 1000.
	/// </summary>
	public int TickInterval { get; set; } = 1000;

	/// <summary>
	/// Whether <see cref="TickOnStart"/> ignores <see cref="StartDelay"/>. Default value is false.
	/// </summary>
	/// <remarks>
	/// If set to false, <see cref="OnTimerTick"/> will not be fired upon the timer being started.
	/// If set to true, <see cref="OnTimerTick"/> will be fired upon the timer being started.
	/// </remarks>
	public bool TickOnStartIgnoreDelay { get; set; } = false;

	/// <summary>
	/// The amount of times <see cref="OnTimerTick"/> can be fired before stopping the timer. Default value is -1 (will tick indefinitely).
	/// </summary>
	public int AmountToTick { get; set; } = -1;

	/// <summary>
	/// The amount of spins to wait. This can reduce high usage on the processor while keeping ticking and delta time accuracy. Default value is 100.
	/// </summary>
	public int AmountToSpinWait { get; set; } = 100;

	/// <summary>
	/// Indicates the current state of the timer.
	/// </summary>
	public TimerState State { get; private set; } = TimerState.Stopped;

	/// <summary>
	/// Determine what ticking method to use. Cannot change ticking method while the timer is running. Call <see cref="Stop"/> first to change the method. Default method is <see cref="TickMethod.TaskDelay"/>.
	/// </summary>
	public TickMethod TickingMethod
	{
		get => tickMethod;
		set
		{
			if (State == TimerState.Running || Enabled)
				throw new InvalidOperationException("Cannot change the tick method while the timer is running.");
			tickMethod = value;
		}
	}

	public delegate void OnTimerStartAfterDelayEventHandler(TimerState oldState, TimerState newState);
	public delegate void OnTimerStartEventHandler(TimerState oldState, TimerState newState);
	public delegate void OnTimerStopEventHandler(TimerState oldState, TimerState newState);
	public delegate void OnTimerTickEventHandler(double deltaTime);
	public delegate void OnTimerPausedEventHandler(TimerState oldState, TimerState newState);
	public delegate void OnTimerResumedEventHandler(TimerState oldState, TimerState newState);
	public delegate void OnTimerStateChangedEventHandler(TimerState oldState, TimerState newState);

	/// <summary>
	/// Fired whenever the timer is started after the delay is finished.
	/// </summary>
	public event OnTimerStartAfterDelayEventHandler OnTimerStartAfterDelay;

	/// <summary>
	/// Fired whenever the timer is started.
	/// </summary>
	public event OnTimerStartEventHandler OnTimerStart;

	/// <summary>
	/// Fired whenever the timer is stopped.
	/// </summary>
	public event OnTimerStopEventHandler OnTimerStop;

	/// <summary>
	/// Fired whenever the interval has elapsed.
	/// </summary>
	public event OnTimerTickEventHandler OnTimerTick;

	/// <summary>
	/// Fired whenever the timer is paused.
	/// </summary>
	public event OnTimerPausedEventHandler OnTimerPaused;

	/// <summary>
	/// Fired whenever the timer is resumed.
	/// </summary>
	public event OnTimerResumedEventHandler OnTimerResumed;

	/// <summary>
	/// Fired whenever the timer state changes.
	/// </summary>
	public event OnTimerStateChangedEventHandler OnTimerStateChanged;

	/// <summary>
	/// Starts the timer.
	/// </summary>
	public void Start()
	{
		if (State == TimerState.Running) return; // we are already running. ignore it
		OnTimerStart?.Invoke(State, TimerState.Running);
		Enabled = true;
		paused = false;
		TimeAtStart = DateTimeOffset.UtcNow;
		InvokeStateChanged(TimerState.Running);
		_ = InternalStart();
	}

	/// <summary>
	/// Stops the <see cref="ExtendedTimer"/>.
	/// </summary>
	/// <remarks>
	/// Calling this will reset the <see cref="TickCount"/> back to 0. <see cref="TimeAtStart"/> does not get reset.
	/// </remarks>
	public void Stop()
	{
		TickCount = 0;
		Enabled = false;
		OnTimerStop?.Invoke(State, TimerState.Stopped);
		InvokeStateChanged(TimerState.Stopped);
	}

	/// <summary>
	/// Stops the <see cref="ExtendedTimer"/> without resetting the <see cref="TickCount"/> back to 0.
	/// </summary>
	/// /// <param name="unpause">How long to wait (in milliseconds) before unpausing the timer.</param>
	public void Pause(int unpause = -1)
	{
		if (paused) return;
		paused = true;
		OnTimerPaused?.Invoke(State, TimerState.Paused);
		InvokeStateChanged(TimerState.Paused);
		if (unpause != -1)
		{
			if (TickingMethod == TickMethod.TaskDelay)
			{
				_ = Task.Run(async () =>
				{
					await Task.Delay(unpause);
					Resume();
				});
			}
			else
			{
				// busy wait until time is up
				while (sw.ElapsedMilliseconds < unpause)
				{
					Thread.SpinWait(AmountToSpinWait);
				}
				Resume();
			}
		}
	}

	/// <summary>
	/// Resumes the timer.
	/// </summary>
	public void Resume()
	{
		paused = false;
		OnTimerResumed?.Invoke(State, TimerState.Running);
		InvokeStateChanged(TimerState.Running);
	}

	private void InvokeOnTickEvent()
	{
		if (!Enabled) return;
		if (paused) return;
		if (TickCount >= AmountToTick && AmountToTick != -1)
		{
			Stop();
			return;
		}
		TickCount++;
		OnTimerTick?.Invoke(sw.Elapsed.TotalSeconds);
		sw.Restart();
	}

	private void InvokeStateChanged(TimerState newState)
	{
		var oldState = State;
		State = newState;
		OnTimerStateChanged?.Invoke(oldState, newState);
	}

	private async Task InternalStart()
	{
		if (TickOnStart && TickOnStartIgnoreDelay)
			InvokeOnTickEvent();

		if (TickingMethod == TickMethod.TaskDelay)
		{
			if (StartDelay > 0)
				await Task.Delay((int)StartDelay);

			OnTimerStartAfterDelay?.Invoke(State, TimerState.Running);

			while (Enabled)
			{
				if (paused)
				{
					await Task.Delay(1);
					continue;
				}
				await Task.Delay(TickInterval);
				InvokeOnTickEvent();
			}
		}
		else
		{
			if (StartDelay > 0)
				while (sw.ElapsedMilliseconds < StartDelay)
				{
					Thread.SpinWait(AmountToSpinWait);
				}

			OnTimerStartAfterDelay?.Invoke(State, TimerState.Running);

			while (Enabled)
			{
				if (sw.Elapsed.TotalMilliseconds >= TickInterval)
				{
					InvokeOnTickEvent();
					sw.Restart();
				}
				else
				{
					Thread.SpinWait(AmountToSpinWait);
				}
			}
		}
	}

	/// <summary>
	/// Free up resources taken by this object.
	/// </summary>
	public void Dispose()
	{
		Stop();
		GC.SuppressFinalize(this);
	}
}
