namespace Azrellie.Meteorology.SPC;

public enum CategoricalRiskType
{
	GeneralThunderstorms = 2,
	Marginal = 3,
	Slight = 4,
	Enhanced = 5,
	Moderate = 6,
	High = 8
}
public enum TornadoRisk
{
	_2Percent = 2,
	_5Percent = 5,
	_10Percent = 10,
	_15Percent = 15,
	_30Percent = 30,
	_45Percent = 45,
	_60Percent = 60
}
public enum WindHailRisk
{
	_5Percent = 5,
	_15Percent = 15,
	_30Percent = 30,
	_45Percent = 45,
	_60Percent = 60
}
public enum RadarProduct
{
	SR_BREF,
	SR_BVEL,
	BDHC,
	BDSA,
	BDOHA
}
public enum OutlookTime
{
	Day1Time0100,
	Day1Time1200,
	Day1Time1300,
	Day1Time1630,
	Day1Time2000,
	Day2Time0600,
	Day2Time1730,
	Day3Time0730
}
public enum ReportType
{
	Tornado = 0,
	Wind = 1,
	Hail = 2
}
public enum WarningEventType
{
	NewIssue,
	Update,
	Cancel,
	Acknowledge,
	Error
}