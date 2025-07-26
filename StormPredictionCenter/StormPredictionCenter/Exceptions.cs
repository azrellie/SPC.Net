namespace Azrellie.Meteorology.SPC;

public class InvalidSPCDayException : Exception
{
	public InvalidSPCDayException() : base() { }
	public InvalidSPCDayException(string message) : base(message) { }
}
public class SPCOutlookDoesntExistException : Exception
{
	public SPCOutlookDoesntExistException() : base() { }
	public SPCOutlookDoesntExistException(string message) : base(message) { }
}
public class SPCWatchDoesntExistOrInvalidWatchNumberException : Exception
{
	public SPCWatchDoesntExistOrInvalidWatchNumberException() : base() { }
	public SPCWatchDoesntExistOrInvalidWatchNumberException(string message) : base(message) { }
}
public class InvalidSPCDateException : Exception
{
	public InvalidSPCDateException() : base() { }
	public InvalidSPCDateException(string message) : base(message) { }
}