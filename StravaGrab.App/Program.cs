using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StravaGrab.App
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                        if (o.OneSlam)
                        {
                            OneSlam();
                        }
                   });

            // we need a routerdb, based on serialisation of PBF data - https://docs.itinero.tech/docs/itinero/data-sources/openstreetmap.html 


           


        }

        // https://github.com/commandlineparser/commandline 
        class Options {
            [Option('o', "oneslam", Required = false, HelpText = "load the one-slam 2021 runs")]
            public bool OneSlam { get; set; }
        }

        static void OneSlam(){
                        string access_token = GetStravaAccessToken();
            int page = 1;
            string url = "https://www.strava.com/api/v3/activities";

            DateTime onefeb = new DateTime(2021,2,1);
            long seconds = new DateTimeOffset(onefeb).ToUnixTimeSeconds();
            string fullrequest = $"{url}?access_token={access_token}&per_page=200&page={page}&after={seconds}";
            double totalkm = 0; 
            double conv_fac = 0.621371;
            double totalseconds = 0;
            CultureInfo provider = CultureInfo.InvariantCulture;
            string format = "d";
            string response;
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

        static string GetStravaAccessToken(){
            var jsonText = File.ReadAllText(@"strava_tokens.json");
            StravaToken tokens = JsonConvert.DeserializeObject<StravaToken>(jsonText); 
            string response;

            if(tokens.Expired) {
                string credsText = File.ReadAllText(@"creds.json");
                StravaCredentials creds = JsonConvert.DeserializeObject<StravaCredentials>(credsText);
                // todo: need to fix this 
                var values = new NameValueCollection();
                values["client_id"] = creds.ClientID.ToString();
                values["client_secret"] = creds.ClientSecret;
                values["grant_type"] = "refresh_token";
                values["refresh_token"] = tokens.refresh_token;
                using(var wb = new WebClient()){
                    response = Encoding.Default.GetString(wb.UploadValues("https://www.strava.com/oauth/token", values));

                }
                tokens = JsonConvert.DeserializeObject<StravaToken>(response);
                File.WriteAllText(@"strava_tokens.json", response);
                tokens = JsonConvert.DeserializeObject<StravaToken>(response);           
            }
            return tokens.access_token;
        }
    }
    class StravaCredentials {
        int _id; 
        string _secret;
        string _code;

        public int ClientID { get => _id; set => _id = value; }
        public string ClientSecret { get => _secret; set => _secret = value; }
        public string ClientCode { get => _code; set => _code = value; }

    }

    class StravaToken 
    {
        string _type;
        string _access;
        long _expires_at;
        uint _expires_in;
        string _refresh;

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
        public string refresh_token { get => _refresh; set => _refresh = value; }

    }
}
