using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StravaGrab.App
{
    class Program
    {
        static void Main(string[] args)
        {
            var jsonText = File.ReadAllText(@"strava_tokens.json");
            StravaToken tokens = JsonConvert.DeserializeObject<StravaToken>(jsonText); 

            if(tokens.Expired) {
                string credsText = File.ReadAllText(@"creds.json");
                StravaCredentials creds = JsonConvert.DeserializeObject<StravaCredentials>(credsText);
                // todo: need to fix this 

            }

            int page = 1;
            string url = "https://www.strava.com/api/v3/activities";

            HttpClient client = new HttpClient();

            DateTime onefeb = new DateTime(2021,2,1);
            long seconds = new DateTimeOffset(onefeb).ToUnixTimeSeconds();
            string fullrequest = $"{url}?access_token={tokens.access_token}&per_page=200&page={page}&after={seconds}";
            string response;
            double totalkm = 0; 
            double conv_fac = 0.621371;
            double totalseconds = 0;
            CultureInfo provider = CultureInfo.InvariantCulture;
            string format = "d";
            using (var wb = new WebClient())
            {
                 response = wb.DownloadString(fullrequest);                    
            }
            dynamic activities = JArray.Parse(response);
            foreach(dynamic activity in activities)
            {
                if(activity.type != "Run")
                    continue;
                string start_date_local = activity.start_date_local;
                DateTime dt = DateTime.ParseExact(start_date_local.Substring(0, 10), format, provider);
                if(dt < onefeb)
                    break;

                double km = activity.distance/1000.0;
                totalkm += km;
                double secs = activity.elapsed_time;
                totalseconds += secs; 

                TimeSpan time = TimeSpan.FromSeconds(secs);
                Console.WriteLine($"{dt.ToShortDateString()}: {km:F2}km/{km*conv_fac:F2}mi {time.ToString(@"hh\:mm\:ss")} [https://www.strava.com/activities/{activity.id}]");
            }
        }
    }

    class StravaCredentials {
        int _id; 
        string _secret;
        string _code;

        public int ID { get => _id; set => _id = value; }
        public string Secret { get => _secret; set => _secret = value; }
        public string Code { get => _code; set => _code = value; }

    }

    class StravaToken 
    {
        string _type;
        string _access;
        long _expires_at;
        uint _expires_in;
        string _request;

        public bool Expired { 
            get {
                // https://stackoverflow.com/questions/2883576/how-do-you-convert-epoch-time-in-c
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(_expires_at);

                DateTime dt = dateTimeOffset.DateTime;

                return dt < DateTime.UtcNow;
            } 
        }

        public string token_type { get => _type; set => _type = value; }
        public string access_token { get => _access; set => _access = value; }
        public long expires_at { get => _expires_at; set => _expires_at = value; }
        public uint expires_in { get => _expires_in; set => _expires_in = value; }
        public string request_token { get => _request; set => _request = value; }

    }
}
