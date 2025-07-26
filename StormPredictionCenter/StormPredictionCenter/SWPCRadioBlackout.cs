namespace Azrellie.Meteorology.SPC;

public record SWPCRadioBlackout
{
	public DateTime TimeOfObservation { get; set; }
	public double Flux { get; set; }
	public string Energy { get; set; }
	public SWPCRadioBlackout(DateTime timeOfObservation, double flux, string energy)
	{
		TimeOfObservation = timeOfObservation;
		Flux = flux;
		Energy = energy;
	}
}