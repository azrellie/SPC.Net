using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using KMLPolygon = SharpKml.Dom.Polygon;
using KMLLineString = SharpKml.Dom.LineString;

namespace Azrellie.Meteorology.SPC;

public class NHC(StormPredictionCenter? self)
{
	private StormPredictionCenter? parent = self;

	/// <summary>
	/// Gets the currently active storms in the Atlantic and Pacific from the National Hurricane Center.
	/// </summary>
	/// <returns>An array containing <see cref="StormObject"/> class which contains the processed data in a more easy to use format.</returns>
	public async Task<StormObject[]> getActiveStorms()
	{
		Dictionary<string, string> timeZoneConvert = new()
		{
			{"AST", "-04:00"},
			{"ADT", "-03:00"},
			{"GMT", "+00:00"},
			{"EST", "-05:00"},
			{"EDT", "-04:00"},
			{"CST", "-06:00"},
			{"CDT", "-05:00"},
			{"MST", "-07:00"},
			{"MDT", "-06:00"},
			{"AZOT", "-01:00"},
			{"AZOST", "+00:00"},
			{"CVT", "-01:00"},
			{"PDT", "-07:00"},
			{"PST", "-08:00"}
		};
		List<StormObject> nationalHurricaneCenterActiveStorms = [];

		string temp = Path.GetTempPath();
		if (!Directory.Exists(temp + "/nhc data"))
			Directory.CreateDirectory(temp + "/nhc data");

		string fileName = temp + "/nhc_active.kml";
		await Utils.downloadFileAsync("https://www.nhc.noaa.gov/gis/kml/nhc_active.kml", fileName);

		if (!Directory.Exists(temp + "/nhc data"))
			Directory.CreateDirectory(temp + "/nhc data");

		foreach (string file in Directory.GetFiles(temp + "/nhc data"))
			File.Delete(file);

		KmlFile kmlFile = KmlFile.Load(File.OpenRead(fileName));
		if (kmlFile.Root is Kml kml)
			if (kml.Feature is Document doc)
				foreach (var feature in doc.Features)
					if (feature is Folder folder)
					{
						if (!folder.Id.Contains("at") && !folder.Id.Contains("ep")) continue; // skip any folders that do not hold hurricane data
						StormObject stormObject = new();
						if (folder.ExtendedData != null)
						{
							foreach (var data in folder.ExtendedData.OtherData)
							{
								string value = data.InnerXml;
								switch (data.Name)
								{
									case "type":
										stormObject.type = value;
										break;
									case "name":
										stormObject.name = value;
										break;
									case "wallet":
										stormObject.wallet = value;
										break;
									case "centerLat":
										stormObject.centerLat = double.Parse(value);
										break;
									case "centerLon":
										stormObject.centerLng = double.Parse(value);
										break;
									case "dateTime":
										string timeZone = string.Empty;
										string timeOffset = string.Empty;
										foreach (KeyValuePair<string, string> pair in timeZoneConvert)
											if (value.Contains(pair.Key))
											{
												timeOffset = pair.Value;
												timeZone = pair.Key;
											}
										stormObject.dateTime = DateTime.Parse(value.Replace(timeZone, timeOffset));
										break;
									case "movement":
										stormObject.movement = value;
										break;
									case "minimumPressure":
										stormObject.minimumPressureMbar = int.Parse(value.Split(' ')[0]);
										break;
									case "maxSustainedWind":
										stormObject.maxSustainedWindsMph = int.Parse(value.Split(' ')[0]);
										break;
									case "headline":
										stormObject.headline = value;
										break;
								}
							}
							nationalHurricaneCenterActiveStorms.Add(stormObject);
						}

						// download other kmz files in this active storms kml file via the network links
						foreach (var subFeature in folder.Features)
							if (subFeature is NetworkLink networkLink)
							{
								if (networkLink.Name == "Past Track")
								{
									string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
									string trackFileName = (temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml").Replace("_best_track", string.Empty);
									await Utils.downloadFileAsync(networkLink.Link.Href.ToString(), trackFileNameKmz);

									ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

									foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
										if (Path.GetExtension(file) != ".kml")
											File.Delete(file);
										else
											File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

									string fileData = File.ReadAllText(trackFileName);
									string modifiedData = fileData.Replace("http://earth.google.com/kml/2.2", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
									File.WriteAllText(trackFileName, modifiedData);

									XmlDocument xmlDoc = new();
									xmlDoc.LoadXml(modifiedData);

									XmlNamespaceManager nsManager = new(xmlDoc.NameTable);
									nsManager.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

									KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
									if (trackKmlData.Root is Kml trackKml)
										if (trackKml.Feature is Document trackDoc)
										{
											foreach (var trackFeature in trackDoc.Features)
											{
												if (trackFeature is Folder trackFolder)
												{
													foreach (var subTrackFeature in trackFolder.Features)
													{
														if (subTrackFeature is Placemark placemark)
														{
															ForecastPoint forecastPoint = new();
															XmlElement? xmlElement = xmlDoc.SelectSingleNode($"//kml:Placemark[kml:name='{placemark.Name}']", nsManager) as XmlElement;
															if (placemark.Geometry is Point point)
															{
																forecastPoint.latitude = point.Coordinate.Latitude;
																forecastPoint.longitude = point.Coordinate.Longitude;
															}
															forecastPoint.stormName = xmlElement["stormName"].InnerText;
															forecastPoint.stormNumber = int.Parse(xmlElement["stormNum"].InnerText);
															forecastPoint.basin = xmlElement["basin"].InnerText;
															forecastPoint.type = xmlElement["stormType"].InnerText;
															forecastPoint.intensityKts = int.Parse(xmlElement["intensity"].InnerText);
															forecastPoint.intensityMph = int.Parse(xmlElement["intensityMPH"].InnerText);
															forecastPoint.intensityKmh = int.Parse(xmlElement["intensityKPH"].InnerText);
															forecastPoint.minSeaLevelPressure = int.Parse(xmlElement["minSeaLevelPres"].InnerText);
															forecastPoint.date = DateTime.ParseExact(xmlElement["dtg"].InnerText, "HHmm 'UTC' MMM dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
															stormObject.pastTrack.Add(forecastPoint);
														}
													}
												}
											}
										}
								}
								else if (networkLink.Name == "Track Forecast")
								{
									string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
									string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
									await Utils.downloadFileAsync(networkLink.Link.Href.ToString(), trackFileNameKmz);

									ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

									foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
										if (Path.GetExtension(file) != ".kml")
											File.Delete(file);
										else
											File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

									KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
									if (trackKmlData.Root is Kml trackKml)
										if (trackKml.Feature is Document trackDoc)
										{
											foreach (var trackFeature in trackDoc.Features)
											{
												if (trackFeature is Folder trackFolder)
												{
													foreach (var subTrackFeature in trackFolder.Features)
													{
														if (subTrackFeature is Placemark placemark)
														{
															ForecastPoint forecastPoint = new();
															if (placemark.Geometry is KMLLineString lineString)
															{
																foreach (Data data in placemark.ExtendedData.Data)
																	if (data.Name == "stormName")
																		forecastPoint.stormName = data.Value;
																	else if (data.Name == "stormType")
																		forecastPoint.type = data.Value;
																	else if (data.Name == "maxWindKnots")
																		forecastPoint.intensityKts = int.Parse(data.Value);
																	else if (data.Name == "maxWindMPH")
																		forecastPoint.intensityMph = int.Parse(data.Value);
																	else if (data.Name == "maxGustKnots")
																		forecastPoint.windGustsKts = int.Parse(data.Value);
																	else if (data.Name == "maxGustMPH")
																		forecastPoint.windGustsMph = int.Parse(data.Value);
																foreach (Vector vec in lineString.Coordinates)
																{
																	forecastPoint.latitude = vec.Latitude;
																	forecastPoint.longitude = vec.Longitude;
																	break;
																}
																stormObject.forecastPoints.Add(forecastPoint);
															}
															if (placemark.Geometry is Point point)
															{
																forecastPoint.latitude = point.Coordinate.Latitude;
																forecastPoint.longitude = point.Coordinate.Longitude;
																string[] trimmedDesc = Regex.Replace(placemark.Description.Text, "</?(tr|td|font|table|b|font color=black|td nowrap|hr)>", string.Empty).Trim().Replace("\t", string.Empty).Split('\n');
																string stormName = trimmedDesc[0];
																string forecastHour = trimmedDesc[3].Trim();
																//DateTime validAt = DateTime.Parse(trimmedDesc[4].Split("Valid at:")[1]);
																string[] winds = trimmedDesc[6].Split("Maximum Wind:")[1].Split('(');
																int maxWindsKts = int.Parse(winds[0].Replace("knots", string.Empty).Trim());
																int maxWindsMph = int.Parse(winds[1].Replace("mph)", string.Empty).Trim());
																string[] windGusts = trimmedDesc[7].Split("Wind Gusts:")[1].Split('(');
																int maxWindGustsKts = int.Parse(windGusts[0].Replace("knots", string.Empty).Trim());
																int maxWindGustsMph = int.Parse(windGusts[1].Replace("mph)", string.Empty).Trim());
																forecastPoint.stormName = stormName;
																forecastPoint.forecastHour = forecastHour;
																//forecastPoint.date = validAt;
																forecastPoint.intensityKts = maxWindsKts;
																forecastPoint.intensityMph = maxWindsMph;
																forecastPoint.windGustsKts = maxWindGustsKts;
																forecastPoint.windGustsMph = maxWindGustsMph;
																bool isHurricane = stormName.Contains("Hurricane");
																bool isStorm = stormName.Contains("Storm");
																bool isDepression = stormName.Contains("Depression");
																string stormType = placemark.StyleUrl.ToString();
																if (stormType == "#initial_point")
																	if (isHurricane)
																		forecastPoint.type = "Hurricane";
																	else if (isStorm)
																		forecastPoint.type = "Storm";
																	else if (isDepression)
																		forecastPoint.type = "Depression";
																if (stormType == "#m_point")
																	forecastPoint.type = "Major Hurricane";
																else if (stormType == "#h_point")
																	forecastPoint.type = "Hurricane";
																else if (stormType == "#s_point")
																	forecastPoint.type = "Storm";
																else if (stormType == "#d_point")
																	forecastPoint.type = "Depression";
																else if (stormType == "#l_point")
																	forecastPoint.type = "Low";
																else if (stormType == "#xm_point" || stormType == "#xh_point" || stormType == "#xs_point" || stormType == "#xd_point")
																	forecastPoint.type = "Post/Potential Tropical Cyclone";
																stormObject.forecastPoints.Add(forecastPoint);
															}
														}
													}
												}
											}
										}
								}
								else if (networkLink.Name == "Cone of Uncertainty")
								{
									string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
									string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
									await Utils.downloadFileAsync(networkLink.Link.Href.ToString(), trackFileNameKmz);

									ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

									foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
										if (Path.GetExtension(file) != ".kml")
											File.Delete(file);
										else
											File.Move(file, temp + "/nhc data/" + Path.GetFileName(file));

									string fileData = File.ReadAllText(trackFileName);
									string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
									File.WriteAllText(trackFileName, modifiedData);

									KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
									if (trackKmlData.Root is Kml trackKml)
										if (trackKml.Feature is Document trackDoc)
										{
											foreach (var trackFeature in trackDoc.Features)
											{
												if (trackFeature is Placemark placemark)
												{
													ForecastCone forecastCone = new();
													if (placemark.Geometry is KMLPolygon polygon)
														foreach (Vector vec in polygon.OuterBoundary.LinearRing.Coordinates)
															forecastCone.coordinates.Add([vec.Latitude, vec.Longitude]);
													foreach (Data data in placemark.ExtendedData.Data)
													{
														if (data.Name == "stormType")
															forecastCone.type = data.Value;
														else if (data.Name == "stormName")
															forecastCone.stormName = data.Value;
														else if (data.Name == "stormNum")
															forecastCone.stormNumber = int.Parse(data.Value);
														else if (data.Name == "advisoryNum")
															forecastCone.advisoryNumber = data.Value;
														else if (data.Name == "basin")
															forecastCone.basin = data.Value;
														else if (data.Name == "advisoryDate")
														{
															string timeZone = string.Empty;
															string timeOffset = string.Empty;
															foreach (KeyValuePair<string, string> pair in timeZoneConvert)
																if (data.Value.Contains(pair.Key))
																{
																	timeOffset = pair.Value;
																	timeZone = pair.Key;
																}
															string restructured = string.Empty;
															string[] split = data.Value.Split(' ');
															string time = string.Empty;
															if (split[0].Length == 3)
																time = split[0].Insert(1, ":");
															else if (split[0].Length == 4)
																time = split[0].Insert(2, ":");
															restructured = $"{time} {split[1]} {split[2].Replace(timeZone, timeOffset)} {split[3]} {split[4]} {split[5]} {split[6]}";
															forecastCone.advisoryDate = DateTime.ParseExact(restructured, "h:mm tt zzz ddd MMM dd yyyy", CultureInfo.InvariantCulture);
														}
														stormObject.forecastCone = forecastCone;
													}
												}
											}
										}
								}
								else if (networkLink.Name == "Initial Extent of Winds")
								{
									string trackFileNameKmz = temp + "/nhc data/" + Path.GetFileName(networkLink.Link.Href.ToString());
									string trackFileName = temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml";
									await Utils.downloadFileAsync(networkLink.Link.Href.ToString(), trackFileNameKmz);

									ZipFile.ExtractToDirectory(trackFileNameKmz, temp + "/nhc data/");

									foreach (string file in Directory.GetFiles(temp + "/nhc data/"))
										if (Path.GetExtension(file) != ".kml")
											File.Delete(file);
										else
											File.Move(file, temp + "/nhc data/" + Path.GetFileNameWithoutExtension(networkLink.Link.Href.ToString()) + ".kml");

									KmlFile trackKmlData = KmlFile.Load(File.OpenRead(trackFileName));
									if (trackKmlData.Root is Kml trackKml)
										if (trackKml.Feature is Document trackDoc)
											foreach (var subTrackFeature in trackDoc.Features)
												if (subTrackFeature is Folder trackFolder)
													foreach (var trackFeature in trackFolder.Features)
													{
														if (trackFeature is Placemark placemark)
														{
															WindRadii windRadii = new();
															if (placemark.Geometry is KMLPolygon polygon)
																foreach (Vector vec in polygon.OuterBoundary.LinearRing.Coordinates)
																	windRadii.coordinates.Add([vec.Latitude, vec.Longitude]);
															stormObject.WindRadii.Add(windRadii);
														}
													}
								}
								foreach (string file in Directory.GetFiles(temp + "/nhc data"))
									File.Delete(file);
							}
					}

		return [..nationalHurricaneCenterActiveStorms];
	}

	/// <summary>
	/// Gets the active storm specified in the Atlantic and Pacific from the National Hurricane Center.
	/// </summary>
	/// <remarks>This calls <see cref="getActiveStorms"/> internally.</remarks>
	/// <returns>A <see cref="StormObject"/> class object containing data about the storm.</returns>
	public async Task<StormObject?> getActiveStorm(string name)
	{
		StormObject[] activeStorms = await getActiveStorms();
		foreach (StormObject storm in activeStorms)
		{
			if (storm.name == name)
				return storm;
		}
		return null;
	}

	/// <summary>
	/// Gets the active storm specified in the Atlantic and Pacific from the National Hurricane Center.
	/// </summary>
	/// <remarks>This version does not call <see cref="getActiveStorms"/> internally. Instead, letting you call <see cref="getActiveStorms"/>, and using that to search through (puts ease on internet connections).</remarks>
	/// <returns>A <see cref="StormObject"/> class object containing data about the storm.</returns>
	public StormObject? getActiveStorm(string name, StormObject[] activeStorms)
	{
		foreach (StormObject storm in activeStorms)
		{
			if (storm.name == name)
				return storm;
		}
		return null;
	}

	/// <summary>
	/// Gets currently active disturbances in the Atlantic or Pacific. (accepted basins: Atlantic, East Pacific, Central Pacific)
	/// </summary>
	/// <param name="basin">The basin to get disturbances from.</param>
	/// <returns>An array containing <see cref="DisturbanceObject"/> objects.</returns>
	public async Task<DisturbanceObject[]> getDisturbances(string basin)
	{
		List<DisturbanceObject> nationalHurricaneCenterDisturbances = [];

		string temp = Path.GetTempPath();
		if (!Directory.Exists(temp + "/nhc data"))
			Directory.CreateDirectory(temp + "/nhc data");

		foreach (string file in Directory.GetFiles(temp + "/nhc data"))
			File.Delete(file);

		if (basin == "Atlantic")
		{
			string fileName = temp + "/nhc data/nhc_disturbances.kmz";
			string kmlFileName = temp + "/nhc data/gtwo_atl.kml";
			await Utils.downloadFileAsync("https://www.nhc.noaa.gov/xgtwo/gtwo_atl.kmz", fileName);

			if (!Directory.Exists(temp + "/nhc data"))
				Directory.CreateDirectory(temp + "/nhc data");

			ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

			string fileData = File.ReadAllText(kmlFileName);
			string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
			File.WriteAllText(kmlFileName, modifiedData);

			KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
			if (kmlFile.Root is Kml kml)
				if (kml.Feature is Document doc)
					foreach (var feature in doc.Features)
						if (feature is Placemark placemark)
						{
							DisturbanceObject disturbance = new();
							if (placemark.Geometry is KMLPolygon polygon)
							{
								SPCPolygon p = new();
								foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
									p.coordinates.Add([g.Latitude, g.Longitude]);
								disturbance.polygon = p;
							}
							else if (placemark.Geometry is Point point)
								disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
							foreach (Data data in placemark.ExtendedData.Data)
								if (data.Name == "Disturbance")
									disturbance.disturbanceIndex = byte.Parse(data.Value);
								else if (data.Name == "2day_percentage")
									disturbance.day2Percentage = data.Value;
								else if (data.Name == "2day_category")
									disturbance.day2Category = data.Value;
								else if (data.Name == "7day_percentage")
									disturbance.day7Percentage = data.Value;
								else if (data.Name == "7day_category")
									disturbance.day7Category = data.Value;
								else if (data.Name == "Discussion")
									disturbance.discussion = data.Value;
							nationalHurricaneCenterDisturbances.Add(disturbance);
						}
		}
		else if (basin == "East Pacific")
		{
			string fileName = temp + "/nhc data/nhc_disturbances.kmz";
			string kmlFileName = temp + "/nhc data/gtwo_pac.kml";
			await Utils.downloadFileAsync("https://www.nhc.noaa.gov/xgtwo/gtwo_pac.kmz", fileName);

			if (!Directory.Exists(temp + "/nhc data"))
				Directory.CreateDirectory(temp + "/nhc data");

			ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

			string fileData = File.ReadAllText(kmlFileName);
			string modifiedData = fileData.Replace("http://earth.google.com/kml/2.1", "http://www.opengis.net/kml/2.2"); // sharpkml doesnt like googles kml namespace
			File.WriteAllText(kmlFileName, modifiedData);

			KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
			if (kmlFile.Root is Kml kml)
				if (kml.Feature is Document doc)
					foreach (var feature in doc.Features)
						if (feature is Placemark placemark)
						{
							DisturbanceObject disturbance = new();
							if (placemark.Geometry is KMLPolygon polygon)
							{
								SPCPolygon p = new();
								foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
									p.coordinates.Add([g.Latitude, g.Longitude]);
								disturbance.polygon = p;
							}
							else if (placemark.Geometry is Point point)
								disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
							foreach (Data data in placemark.ExtendedData.Data)
								if (data.Name == "Disturbance")
									disturbance.disturbanceIndex = byte.Parse(data.Value);
								else if (data.Name == "2day_percentage")
									disturbance.day2Percentage = data.Value;
								else if (data.Name == "2day_category")
									disturbance.day2Category = data.Value;
								else if (data.Name == "7day_percentage")
									disturbance.day7Percentage = data.Value;
								else if (data.Name == "7day_category")
									disturbance.day7Category = data.Value;
								else if (data.Name == "Discussion")
									disturbance.discussion = data.Value;
							nationalHurricaneCenterDisturbances.Add(disturbance);
						}
		}
		else if (basin == "Central Pacific")
		{
			string fileName = temp + "/nhc data/nhc_disturbances.kmz";
			string kmlFileName = temp + "/nhc data/gtwo_cpac.kml";
			await Utils.downloadFileAsync("https://www.nhc.noaa.gov/xgtwo/gtwo_cpac.kmz", fileName);

			if (!Directory.Exists(temp + "/nhc data"))
				Directory.CreateDirectory(temp + "/nhc data");

			ZipFile.ExtractToDirectory(fileName, temp + "/nhc data");

			string fileData = File.ReadAllText(kmlFileName);
			string modifiedData = fileData.Replace("<kml xmlns:gx=\"http://www.google.com/kml/ext/2.2\" xmlns=\"http://earth.google.com/kml/2.1\">", "<kml xmlns=\"http://www.opengis.net/kml/2.2\">"); // sharpkml doesnt like googles kml namespace
			modifiedData = modifiedData.Replace("gx:", string.Empty);
			File.WriteAllText(kmlFileName, modifiedData);

			KmlFile kmlFile = KmlFile.Load(File.OpenRead(kmlFileName));
			if (kmlFile.Root is Kml kml)
				if (kml.Feature is Document doc)
					foreach (var feature in doc.Features)
						if (feature is Placemark placemark)
						{
							if (placemark.ExtendedData == null) continue;
							DisturbanceObject disturbance = new();
							if (placemark.Geometry is KMLPolygon polygon)
							{
								SPCPolygon p = new();
								foreach (Vector g in polygon.OuterBoundary.LinearRing.Coordinates)
									p.coordinates.Add([g.Latitude, g.Longitude]);
								disturbance.polygon = p;
							}
							else if (placemark.Geometry is Point point)
								disturbance.point = new(point.Coordinate.Latitude, point.Coordinate.Longitude);
							foreach (Data data in placemark.ExtendedData.Data)
								if (data.Name == "Disturbance")
									disturbance.disturbanceIndex = byte.Parse(data.Value);
								else if (data.Name == "2day_percentage")
									disturbance.day2Percentage = data.Value;
								else if (data.Name == "2day_category")
									disturbance.day2Category = data.Value;
								else if (data.Name == "7day_percentage")
									disturbance.day7Percentage = data.Value;
								else if (data.Name == "7day_category")
									disturbance.day7Category = data.Value;
								else if (data.Name == "Discussion")
									disturbance.discussion = data.Value;
							nationalHurricaneCenterDisturbances.Add(disturbance);
						}
		}
		return [..nationalHurricaneCenterDisturbances];
	}
}