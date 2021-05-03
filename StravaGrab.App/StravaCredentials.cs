// using Itinero.Optimization;

namespace StravaGrab.App
{
    class StravaCredentials {
        int _id; 
        string _secret;
        string _code;

        public int ClientID { get => _id; set => _id = value; }
        public string ClientSecret { get => _secret; set => _secret = value; }
        public string ClientCode { get => _code; set => _code = value; }

    }
}
