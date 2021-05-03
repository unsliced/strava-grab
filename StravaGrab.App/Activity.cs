using System;
using System.Globalization;

namespace StravaGrab.App
{
    class Activity {
        DateTime dt;
        string _type;
        double _km;
        TimeSpan _time;
        string _id;

        public double Distance => _km;

        public Activity(dynamic activity) {
            _type = activity.type;
            string start_date_local = activity.start_date_local;

            dt = DateTime.ParseExact(start_date_local.Substring(0, 10), "d", CultureInfo.InvariantCulture);
            _km = activity.distance/1000.0;
            double secs = activity.elapsed_time;

            _time = TimeSpan.FromSeconds(secs);
            _id = activity.id;

        }

        // [startDate, endDate)
        public bool Qualifying(bool onlyRunning, DateTime startDate, DateTime? endDate = null) {
            return (!onlyRunning || (onlyRunning && _type == "Run")) && dt >= startDate && (!endDate.HasValue || dt < endDate.Value);
        }

        public override string ToString()
        {
            double conv_fac = 0.621371; // todo: euch. is there a better, built-in conversion routine?

            return ($"{dt.ToShortDateString()}: {_km:F2}km/{_km*conv_fac:F2}mi {_time.ToString(@"hh\:mm\:ss")} [https://www.strava.com/activities/{_id}]");
        }
    }
}
