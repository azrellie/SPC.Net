namespace Azrellie.Meteorology.SPC;

public record SWPCKIndex
{
	public DateTime TimeOfObservation { get; set; }
	public double KPIndex { get; set; }
	public double EstimatedKPIndex { get; set; }
	public string KP { get; set; }
	public SWPCKIndex(DateTime timeOfObservation, double kpIndex, double estimatedKPIndex, string kp)
	{
		TimeOfObservation = timeOfObservation;
		KPIndex = kpIndex;
		EstimatedKPIndex = estimatedKPIndex;
		KP = kp;
	}
}