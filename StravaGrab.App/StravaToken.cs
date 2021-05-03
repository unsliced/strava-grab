using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace StravaGrab.App
{
    class StravaToken 
    {
        string _type;
        string _access;
        long _expires_at;
        uint _expires_in;
        string _refresh;

        bool Expired { 
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

        public static string GetStravaAccessToken(){
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

    
}
