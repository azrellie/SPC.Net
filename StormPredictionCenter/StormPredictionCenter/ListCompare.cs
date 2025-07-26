namespace Azrellie.Meteorology.SPC;

public class WatchComparer : IEqualityComparer<StormPredictionCenterWatch>
{
	public bool Equals(StormPredictionCenterWatch watch1, StormPredictionCenterWatch watch2)
	{
		if (watch2 is null || watch1 is null) return false;
		return watch1.watchNumber == watch2.watchNumber; // if the watch number is the same, then everything else will be the same also
	}

	public int GetHashCode(StormPredictionCenterWatch obj)
	{
		return HashCode.Combine(obj.watchNumber);
	}
}
public class WatchBoxComparer : IEqualityComparer<StormPredictionCenterWatchBox>
{
	public bool Equals(StormPredictionCenterWatchBox watch1, StormPredictionCenterWatchBox watch2)
	{
		if (watch2 is null || watch1 is null) return false;
		return watch1.watchNumber == watch2.watchNumber; // if the watch number is the same, then everything else will be the same also
	}

	public int GetHashCode(StormPredictionCenterWatchBox obj)
	{
		return HashCode.Combine(obj.watchNumber);
	}
}