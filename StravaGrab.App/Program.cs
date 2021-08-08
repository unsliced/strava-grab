using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using CommandLine;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Itinero;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StravaGrab.App
{
    // todo: install on a Pi4: https://docs.microsoft.com/en-us/dotnet/iot/deployment 
    // todo: extract personal information to a git ignored json file   
    partial class Program
    {
        static void Main(string[] args)
        {
//            CyclingWeekly();
//             Gvrat21();
//            Export2Mongo("mongodb://192.168.1.17:27017/", 2020);
            UpdateMongo("mongodb://192.168.1.17:27017/", true);

            Parser
                .Default
                .ParseArguments<CommandLineOptions>(args)
                .WithParsed<CommandLineOptions>(o =>
                   {
                        if (o.OneSlam)
                            OneSlam2021();
                        if(o.YearToDate)
                            AnnualDistance(false);
                        if(o.SerialiseRouter)
                            SerialiseRouter();
                        if(o.Gvrat)
                            Gvrat21();
                        if(o.CycleWeekly)
                            CyclingWeekly();
                        if(!string.IsNullOrEmpty(o.Destination)) 
                            HomeTo(o.Destination, o.CacheDestination);
                        if(!string.IsNullOrEmpty(o.GeoJson) && !string.IsNullOrEmpty(o.Js))
                            AnnualProgress(o.GeoJson, o.Js, o.Debug);
                       if(o.Mongo)
                            UpdateMongo("mongodb://192.168.1.17:27017/", true);
                   });

            // todo: need to route of approx. 3650km 
        }

        static void CalculatePremiershipTour(bool output = false) {
            // find the locations for the premiership grounds, and Twickenham 
            // calculate and dump the route of a tour, 
            // presumably ending at Twickenham for the final ... 

            IDictionary<string, Tuple<float,float>> prem = new Dictionary<string, Tuple<float,float>>();
            prem.Add("stoop", new Tuple<float,float>(51.44944f, -0.34369f));
            prem.Add("welford", new Tuple<float,float>(52.624167f, -1.133056f));
            prem.Add("ricoh", new Tuple<float,float>(52.448056f, -1.495556f));
            prem.Add("franklins", new Tuple<float,float>(52.2393f, -0.9196f));
            prem.Add("kingston", new Tuple<float,float>(55.018611f, -1.672222f));
            prem.Add("sixways", new Tuple<float,float>(52.215556f, -2.1625f));
            prem.Add("sandy", new Tuple<float,float>(50.709308f, -3.467572f));
            prem.Add("ajbell", new Tuple<float,float>(53.469444f, -2.375f));
            prem.Add("rec", new Tuple<float,float>(51.382222f, -2.355278f));
            prem.Add("ashtongate", new Tuple<float,float>(51.44f, -2.620278f));
            prem.Add("brentford", new Tuple<float,float>(51.4906f, -0.2889f));
            prem.Add("kingsholm", new Tuple<float,float>(51.871667f, -2.242778f));


            RouterDb rdb;
            using(FileStream fs = new FileInfo(@"pbf/gbr.routerdb").OpenRead())
                rdb = RouterDb.Deserialize(fs);


            Router router = new Router(rdb);
            Itinero.Profiles.Profile p = Itinero.Osm.Vehicles.Vehicle.Pedestrian.Shortest();

            IDictionary<string, RouterPoint> points = 
                prem.Select(t => t)
                    .ToDictionary(t => t.Key, t => router.Resolve(p, t.Value.Item1, t.Value.Item2, 250));


            IDictionary<Tuple<string,string>, double> lookups = new Dictionary<Tuple<string,string>, double>();
            foreach(string f in points.Keys.OrderBy(v => v)) {
                foreach(string t in points.Keys.Where(t => t.CompareTo(f) > 0)) { 


                    Route route = router.Calculate(Vehicle.Pedestrian.Shortest(), points[f], points[t]);       
                    lookups.Add(new Tuple<string, string>(f,t), route.TotalDistance);
                    if(output)     
                        Console.WriteLine($"{f}:{t} = {route.TotalDistance}");
                }
            }

            // MST (e.g. via Prim's/Kruskal's) then a DFS bound by the cost of the MST 
            // https://cs.stackexchange.com/a/1805 
            IList<string> visited = new List<string> ();
            IList<Tuple<string,string>> pairs = new List<Tuple<string,string>>();
            while(visited.Count < points.Count) {
                double min = lookups.Select(t => t.Value).Max();
                Tuple<string, string> t = null;
                foreach(KeyValuePair<Tuple<string, string>, double> kvp in lookups) { 
                    if(kvp.Value < min && 
                        (visited.Count == 0 || visited.Contains(kvp.Key.Item1) || visited.Contains(kvp.Key.Item2)) && 
                        !(visited.Contains(kvp.Key.Item1)  && visited.Contains(kvp.Key.Item2)))  {
                        t = kvp.Key;
                        min = kvp.Value;
                    }
                }
                if(!visited.Contains(t.Item1))
                    visited.Add(t.Item1);
                if(!visited.Contains(t.Item2))
                    visited.Add(t.Item2);
                pairs.Add(t);
            }

            double mst = pairs.Select(p => lookups[p]).Sum() * 2;

            Console.WriteLine($"minimal spanning tree/upper bound: {mst}"); 

            // now for a DFS bounded by the MST 
            


            // todo: get https://github.com/itinero/optimization to work. 
            // var res = router.CalculateTSP(Vehicle.Pedestrian.Shortest(), prem.Select(kvp => kvp.Value).Select(t => new Coordinate(t.Item1, t.Item2)).ToArray());

//            ISet<int> iset = new HashSet<int>();
  //          float[][] matrix = router.CalculateWeight(p, points.ToArray(), iset); 


//            string fn = $"pbf//premiership.geojson";
//            using (var writer = new StreamWriter(fn)) {
//                res.WriteGeoJson(writer);
//            }
        }

        static void AnnualProgress(string geojsonfilename, string jsfilename, bool output = false) {
            double a = AnnualTotal(true, false);
            UpdateProgress(a, geojsonfilename, jsfilename, "Year to Date", output);
        }

        // nabbed from https://stackoverflow.com/a/11155102/2902
        // This presumes that weeks start with Monday.
        // Week 1 is the 1st week of the year with a Thursday in it.
        public static int GetIso8601WeekOfYear(DateTime time)
        {
            // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
            // be the same week# as whatever Thursday, Friday or Saturday are,
            // and we always get those right
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }

            // Return the week of our adjusted day
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        } 

        // based on https://stackoverflow.com/questions/662379/calculate-date-from-week-number
        public static DateTime LastDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            // Use first Thursday in January to get first week of the year as
            // it will never be in Week 52/53
            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var weekNum = weekOfYear;
            // As we're adding days to a date in Week 1,
            // we need to subtract 1 in order to get the right date for week #1
            if (firstWeek == 1)
            {
                weekNum -= 1;
            }

            // Using the first Thursday as starting week ensures that we are starting in the right year
            // then we add number of weeks multiplied with days
            var result = firstThursday.AddDays(weekNum * 7);

            // Add 3 days from Thursday to get the Sunday, which is the last day of the week containing the first weekday in ISO8601
            return result.AddDays(3);
        }       

        static void CyclingWeekly(bool lastyear = false) { 
            int offset = lastyear ? 1 : 0;

            DateTime startDate = new DateTime(DateTime.Today.Year-offset,1,1);
            DateTime endDate = lastyear ? new DateTime(DateTime.Today.Year,1,1).AddDays(-1) : DateTime.Today;
            IEnumerable<Activity> activities = ListOfActivities(startDate, endDate, false).Where(a => a.IsBike).OrderBy(a => a.Date);
            IList<double> buckets = Enumerable.Repeat(0d, 53).ToList(); 
            int w = 0;
            
            foreach(Activity a in activities) {
                int i = GetIso8601WeekOfYear(a.Date)-1;
                buckets[i] += a.Distance;
                if(i > w)
                    w = i;
            }
            for(int i = 0; i <= w; ++i) {
                DateTime dt = LastDateOfWeekISO8601(DateTime.Today.Year, i+1).AddDays(6);
                Console.WriteLine($"{dt.ToShortDateString()}: {buckets[i]:F2}");
            }
        }

        static void UpdateMongo(string clientName, bool trace) {
            var client = new MongoClient(clientName);
            var database = client.GetDatabase("running");
            var collection = database.GetCollection<BsonDocument>("summary");

            // get the latest date - https://stackoverflow.com/questions/32076382/mongodb-how-to-get-max-value-from-collections
            var lastYearFilter = Builders<BsonDocument>.Filter.Gte("dt", new BsonDateTime(DateTime.Today.AddYears(-1)));
            var sort = Builders<BsonDocument>.Sort.Descending("dt");
            var lastyear = collection.Find(lastYearFilter).Sort(sort);
            var mostrecent = lastyear.First().AsBsonDocument;
            DateTime dt;
            try {
                dt = mostrecent["dt"].AsBsonDateTime.ToUniversalTime();      
            } catch(InvalidCastException){
                dt = DateTime.Today.AddYears(-1);
            }

            // get the ids for those within a week 

            var recentFilter = Builders<BsonDocument>.Filter.Gte("dt", dt.AddDays(-7));
            var projection = Builders<BsonDocument>.Projection.Include("strava_id");
            var recentIDs = collection.Find(recentFilter).Project(projection).ToList().Select(x => x["strava_id"]).ToList();

            // ask strava for a week before the latest date
            IEnumerable<Activity> activities = ListOfActivities(dt, null, true).OrderBy(a => a.Date);

            IList<BsonDocument> toAdd = new List<BsonDocument>();
            foreach(Activity a in activities)
            {
                if(!recentIDs.Contains(a.StravaId)) {
                    toAdd.Add(a.ToBsonDocument());
                }
            }

            // add any for which the ids is not available 
            if(toAdd.Count > 0) 
                collection.InsertMany(toAdd);
            if(trace)
                Console.WriteLine($"added {toAdd.Count} entries");

        }

        static void Export2Mongo(string clientName, int? year = null) {
            DateTime startDate = new DateTime(year.HasValue ? year.Value : DateTime.Today.Year,1,1);
            DateTime endDate = new DateTime(year.HasValue ? year.Value : DateTime.Today.Year,12,31);

            // todo: if it's this year look at the db to get the new starting date - make sure you don't add any ids that are already in there 
            IEnumerable<Activity> activities = ListOfActivities(startDate, endDate, true).OrderBy(a => a.Date);

            var client = new MongoClient(clientName);
            var database = client.GetDatabase("running");
            var collection = database.GetCollection<BsonDocument>("summary");

            foreach(Activity a in activities) 
            {
                BsonDocument doc = a.ToBsonDocument();
                collection.InsertOne(doc);
            }

        }

        static void Gvrat21(bool js = false) {
            DateTime startDate = new DateTime(DateTime.Today.Year,5,1);
            DateTime endDate = new DateTime(DateTime.Today.Year,9,1);
            IEnumerable<Activity> activities = ListOfActivities(startDate, endDate, true).OrderBy(a => a.Date);
            double totalkm = 0d;
            int counter = 0;
            foreach(Activity a in activities) 
            {
                Console.WriteLine(a);
                totalkm += a.Distance;
                ++counter;
            }
            int inches = 40734144; // at least that's what Laz says - https://gvrat.racing/faq/ 
            double targetKilometres = (inches * 2.54) / (100 * 1000);
            Console.WriteLine($"{counter} activities");
            Console.WriteLine($"Progress: {totalkm:F3}km ({totalkm*100/targetKilometres:F1}%)");
            double diff = (DateTime.Now - startDate).TotalDays * targetKilometres / totalkm;
            Console.WriteLine($"Estimated finish: {startDate.AddDays(diff).ToShortDateString()}");

            double diff2 = (DateTime.Now - startDate).TotalDays * (1000 * Activity.KilometreToMiles) / totalkm;
            Console.WriteLine($"Estimated 1000 finish: {startDate.AddDays(diff2).ToShortDateString()}");

            double diff3 = (DateTime.Now - startDate).TotalDays * (2 * targetKilometres) / totalkm;
            Console.WriteLine($"Estimated BAT finish: {startDate.AddDays(diff3).ToShortDateString()}");



            if(js)
                // todo: n.b. the feature collection in the downloaded GeoJson needs some TLC. 
                UpdateProgress(totalkm, "gvrat", "gvrat21", "Distance since May 1, true");
        }

        static void UpdateProgress(double km, string geojsonfilename, string jsfilename, string msg, bool output = false) {
            // grab the strava total 

            if(output)
                Console.WriteLine($"run distance: {km:F2}");

            // get the route 
            string json = File.ReadAllText($"pbf/{geojsonfilename}.geojson");   

            var featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(json);
            //todo: last two features are first and last 
            Feature current = null;
            bool completed = false;
            Feature last = featureCollection.Features.Last();
            foreach(Feature f in featureCollection.Features) {
                dynamic d = f.Properties["distance"];
                double dst = Double.Parse(d)/1000.0f;
                if(current == null && dst > km)
                    current = f;
                // todo: for each day add a marker with date and progress. 
                // todo: include a target over the period? 

            }

            if(current == null) {
                completed = true;
                current = last;
            }

            double distance = double.Parse(last.Properties["distance"].ToString())/1000.0f;

            IGeometryObject geo = current.Geometry;
            IPosition pos;
            if(completed) {
                pos = ((Point)geo).Coordinates;
            }
            else {
                IReadOnlyCollection<IPosition> coll = ((LineString)geo).Coordinates;
                pos = coll.Last();
            }
            if(output)
                Console.WriteLine($"{completed} / {pos.Latitude}, {pos.Longitude} / {(100*km/distance):F2}%");

            // write the JS file
            // todo: add the current position as a green style 
            string js = $"var marker = L.marker([{pos.Latitude}, {pos.Longitude}]).addTo(mymap);{Environment.NewLine}marker.bindPopup(\"{msg}: <b>{km:F2}km</b><br>I am <b>{(100d*km/distance):F2}%</b> complete.\").openPopup();";

            File.WriteAllText($"{jsfilename}.js", js);


        }

        static void HomeTo(string destination, bool cacheDestination) {
            FromTo("home", destination, cacheDestination);
        }

        /* from the dictionary, create a point to point GeoJson route, for export to a file which can be 
         * read into, for example. an OSM map hosted on a web page.
         */
        static void FromTo(string source, string destination, bool cacheDestination) {
            IDictionary<string, Tuple<float,float>> rps = new Dictionary<string, Tuple<float,float>>();
            rps.Add("landsend", new Tuple<float,float>(50.0662633f, -5.7148222f));
            rps.Add("john-o-groats", new Tuple<float,float>(58.6439493f, -3.0254357f));
            
            RouterDb rdb;
            // todo: take the routerdb name as an argument 
            using(FileStream fs = new FileInfo(@"pbf/gbr.routerdb").OpenRead())
                rdb = RouterDb.Deserialize(fs);

            Router router = new Router(rdb);
            Tuple<float, float> h = rps[source];
            Tuple<float, float> d = rps[destination];

            Route route = router.Calculate(Vehicle.Pedestrian.Shortest(), h.Item1, h.Item2, d.Item1, d.Item2);            

            if(cacheDestination) {
                string fn = $"pbf//{source}-{destination}.geojson";
                using (var writer = new StreamWriter(fn)) {
                    route.WriteGeoJson(writer);
                    // todo: can we deserialise the route? 
                }
            }
/* POTENTIALLY USEFUL SNIPPETS: 


            foreach(RoutePosition rp in route) 
            {
                var s = route.Shape[rp.Shape];
                var sm = route.ShapeMeta[rp.Shape];
                Console.Write($"{(sm.Distance/1000.0):0.##}/{sm.Time} [");
                foreach(var a in sm.Attributes) 
                    Console.Write($"{a.Key}:{a.Value} ");
                Console.WriteLine("]");

            }

            foreach(var sm in route.ShapeMeta){
                var s = route.Shape[sm.Shape];
                var a = sm.Attributes;
                // todo: time in seconds to mm:ss 
                string name;
                if(!a.TryGetValue("name", out name))
                    name = string.Empty;
                Console.WriteLine($"{(sm.Distance/1000.0):0.##}/{sm.Time} {name}");
            }            
*/
        }

        // This doesn't need to be run very often - serialise the pbf into a routerdb format. Quite slow. 
        static void SerialiseRouter() {
            // https://docs.itinero.tech/docs/itinero/data-sources/openstreetmap.html 
            RouterDb rdb = new RouterDb();
            // e.g. https://download.geofabrik.de/europe/great-britain.html
            using(FileStream pbf = new FileInfo(@"pbf/tennessee-latest.osm.pbf").OpenRead()) {
                rdb.LoadOsmData(pbf, Vehicle.Pedestrian);
            }

            var profile = Vehicle.Pedestrian.Shortest();
            rdb.AddContracted(profile);
            rdb.AddContracted(Vehicle.Car.Fastest());
            rdb.AddContracted(Vehicle.Bicycle.Shortest());

            using(FileStream outfs = new FileInfo(@"pbf/tn.routerdb").Open(FileMode.Create)) {
                rdb.Serialize(outfs);
            }

        }

        static void AnnualDistance(bool rolling) {
            double res = AnnualTotal(true, rolling);
            string desc = rolling ? "last 365 days" : "year-to-date";
            Console.WriteLine($"Annual Progress ({desc}): {res:F3}");
        }

        // Get the activities and return just their sum. 
        static double AnnualTotal(bool onlyRunning = true, bool rolling = false) {
            DateTime startDate = rolling ? DateTime.Today.AddYears(-1) : new DateTime(DateTime.Today.Year,1,1);
            return ListOfActivities(startDate, null, onlyRunning).Select(a => a.Distance).Sum();
        }

        // 400 miles in February/March 2021: https://centurionrunning.com/reports/2021/one-slam-2021-race-report
        static void OneSlam2021() 
        {
            IList<Activity> activities = ListOfActivities(new DateTime(2021,2,1), new DateTime(2021,4,1), true);
            double totalkm = 0d;
            foreach(Activity a in activities) 
            {
                Console.WriteLine(a);
                totalkm += a.Distance;

            }
            Console.WriteLine($"total distance(km): {totalkm:F3}");
        }

        static IList<Activity> ListOfActivities(DateTime startingFrom, DateTime? endingAt, bool onlyRunning = true){
            int page = 1;            
            long seconds = new DateTimeOffset(startingFrom).ToUnixTimeSeconds();
            IList<Activity> rv = new List<Activity>();
            while(true) {
                string request = $"per_page=200&page={page}&after={seconds}";
                if(endingAt.HasValue) {
                    long seconds2 = new DateTimeOffset(endingAt.Value).ToUnixTimeSeconds();
                    request += $"&before={seconds2}";                        
                }
                string response = GetStravaResponse(request);
                dynamic activities = JArray.Parse(response);
                if(activities.Count == 0)
                    break;
                foreach(dynamic activity in activities)
                {
                    Activity a = new Activity(activity);
                    if(!a.Qualifying(onlyRunning, startingFrom, endingAt))
                        continue;                   

                    rv.Add(a); 
                }
                ++page;
            }
            
            return rv;
        }   

        static string GetStravaResponse(string request) {
            string url = "https://www.strava.com/api/v3/activities";
            string access_token = StravaToken.GetStravaAccessToken();
            string response = string.Empty;

            try {
                using (var wb = new WebClient())
                {
                    response = wb.DownloadString($"{url}?access_token={access_token}&{request}");
                }
            } catch(Exception e) {
                Console.WriteLine($"Problem hitting Strava. You are connected?");
                Console.WriteLine($"{e.Message}");
                Console.WriteLine($"{response}");
                throw;
            }
            return response;
        }

//        static IList<StravaLap> GetActivityLaps(string id )
        
    }
}
