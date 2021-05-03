using System;
using System.Collections.Generic;
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

namespace StravaGrab.App
{
    // todo: install on a Pi4: https://docs.microsoft.com/en-us/dotnet/iot/deployment 
    // todo: extract personal information to a git ignored json file   
    partial class Program
    {
        static void Main(string[] args)
        {
            Gvrat21();

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
                        if(!string.IsNullOrEmpty(o.Destination)) 
                            HomeTo(o.Destination, o.CacheDestination);
                        if(!string.IsNullOrEmpty(o.GeoJson) && !string.IsNullOrEmpty(o.Js))
                            AnnualProgress(o.GeoJson, o.Js, o.Debug);
                       
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

            // todo: get https://github.com/itinero/optimization to work. 
            // var res = router.CalculateTSP(Vehicle.Pedestrian.Shortest(), prem.Select(kvp => kvp.Value).Select(t => new Coordinate(t.Item1, t.Item2)).ToArray());

            var points = prem.Select(kvp => kvp.Value).Select(t => router.Resolve(p, t.Item1, t.Item2, 250)).ToList();
            ISet<int> iset = new HashSet<int>();
            float[][] matrix = router.CalculateWeight(p, points.ToArray(), iset); 

            // MST (e.g. via Prim's/Kruskal's) then a DFS bound by the cost of the MST 
            // https://cs.stackexchange.com/a/1805 

//            string fn = $"pbf//premiership.geojson";
//            using (var writer = new StreamWriter(fn)) {
//                res.WriteGeoJson(writer);
//            }
        }

        static void AnnualProgress(string geojsonfilename, string jsfilename, bool output = false) {
            double a = AnnualTotal(true, false);
            UpdateProgress(a, geojsonfilename, jsfilename, "Year to Date", output);
        }

        static void Gvrat21() {
            DateTime startDate = new DateTime(DateTime.Today.Year,5,1);
            DateTime endDate = new DateTime(DateTime.Today.Year,9,1);
            double km = ListOfActivities(startDate, endDate, true).Select(a => a.Distance).Sum();
            // todo: n.b. the feature collection in the downloaded GeoJson needs some TLC. 
            UpdateProgress(km, "gvrat", "gvrat21", "Distance since May 1, true");
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
            string access_token = StravaToken.GetStravaAccessToken();
            int page = 1;
            string url = "https://www.strava.com/api/v3/activities";

            
            long seconds = new DateTimeOffset(startingFrom).ToUnixTimeSeconds();
            IList<Activity> rv = new List<Activity>();
            while(true) {
                string fullrequest = $"{url}?access_token={access_token}&per_page=200&page={page}&after={seconds}";
                if(endingAt.HasValue) {
                    long seconds2 = new DateTimeOffset(endingAt.Value).ToUnixTimeSeconds();
                    fullrequest += $"&before={seconds2}";
                }
                string response;
                using (var wb = new WebClient())
                {
                    response = wb.DownloadString(fullrequest);                    
                }
                dynamic activities = JArray.Parse(response);
                Console.WriteLine($"activites: {activities.Count}");
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
    }
}
