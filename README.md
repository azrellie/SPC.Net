### *This repo has been renamed to SPC.Net previously named StormPredictionCenter.*

# Storm Prediction Center API for C#

### Latest verison: 2.0.0

This API was made for the need of me constantly having to access data
from the Storm Prediction Center, most notably the convective outlooks,
tornado watches, severe thunderstorm watches, and mesoscale discussions.

The data that this API uses comes from the listed pages:
1. https://www.spc.noaa.gov/gis/
2. https://www.spc.noaa.gov/archive/
3. https://www.weather.gov/documentation/services-web-api#/
4. https://www.spc.noaa.gov/products/watch/ww0119.html (at the time of typing this)
5. https://www.wpc.ncep.noaa.gov/kml/kmlproducts.php
6. https://www.nhc.noaa.gov/gis/
7. https://mrms.ncep.noaa.gov/data/RIDGEII/

Said data is gathered up and processed to be used for whatever it is
needed for without the hassle of retrieving the data, and processing it.

This API can be used for many various things that use C# as its language.
Whether thats software for Windows, games for Unity, or even for addons/mods
that use C# to develop said addons/mods.

This API was developed on the .NET 8 SDK. Backports are unlikely since I do not really
have the time for that (but you are free to do it your self if you so wish).

Porting to other languages like C++, python, javascript etc are possible, but the same statement
above still applies.

None of the classes within this API are nullable, which means you can safely read the
fields and properties of the classes without having to do null checks, as null checks
are handled internally and any null values will instead just use their default value.

This API is still brand new and may have some bugs associated with it.

Planned features for API may include:
1. Add a class to decode raw radar data/generate images from raw radar data using specified color tables.
2. Have a separate class for things solely related to the National Weather Service, such as observations and forecasts.
