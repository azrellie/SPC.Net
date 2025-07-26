namespace Azrellie.Meteorology.SPC;

public record SWPCSolarWind
{
	public DateTime TimeOfObservation { get; set; }
	public double Speed { get; set; }
	public double Density { get; set; }
	public double Temperature { get; set; }

	public SWPCSolarWind(DateTime time, double speed, double density, double temp)
	{
		TimeOfObservation = time;
		Speed = speed;
		Density = density;
		Temperature = temp;
	}
}