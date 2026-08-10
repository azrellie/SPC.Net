namespace Azrellie.Meteorology.SPC;

public record SWPCRadioBlackout
{
	public DateTime TimeOfObservation { get; set; }
	public double Flux { get; set; }
	public double ObservedFlux { get; set; }
	public string Energy { get; set; }
	public SWPCRadioBlackout(DateTime timeOfObservation, double flux, double obsFlux, string energy)
	{
		TimeOfObservation = timeOfObservation;
		Flux = flux;
		ObservedFlux = obsFlux;
		Energy = energy;
	}
}