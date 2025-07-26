namespace Azrellie.Meteorology.SPC;

public record SWPC10_7cmRadioFlux
{
	public DateTime TimeOfObservation { get; set; }
	public double Frequency { get; set; }
	public double Flux { get; set; }
}