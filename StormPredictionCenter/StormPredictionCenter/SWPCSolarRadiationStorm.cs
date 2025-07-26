namespace Azrellie.Meteorology.SPC;

public record SWPCSolarRadiationStorm
{
	public DateTime TimeOfObservation { get; set; }
	public double ProtonFlux { get; set; }
	public string Energy { get; set; }
	public SWPCSolarRadiationStorm(DateTime timeOfObservation, double protonFlux, string energy)
	{
		TimeOfObservation = timeOfObservation;
		ProtonFlux = protonFlux;
		Energy = energy;
	}
}