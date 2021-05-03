using CommandLine;

namespace StravaGrab.App
{
    partial class Program
    {
        // https://github.com/commandlineparser/commandline 
        class CommandLineOptions {

            [Option('o', "oneslam", Required = false, HelpText = "load the one-slam 2021 runs")]
            public bool OneSlam { get; set; }
            [Option('g', "gvrat", Required = false, HelpText = "load the GVRAT 2021 runs")]
            public bool Gvrat { get; set; }

            [Option('r', "router", Required = false, HelpText = "serialise the PBF file to a routerdb object")]
            public bool SerialiseRouter { get; set; }

            [Option('d', "destination", Required = false, HelpText = "from home to ... ")]
            public string Destination { get;set;}

            [Option('c', "cache", Required = false, HelpText = "cache the destination")]
            public bool CacheDestination { get; set; }

            [Option("geojson", Required = false, HelpText = "compare against the given route")]
            public string GeoJson { get; set; }

            [Option("js", Required = false, HelpText = "write to the given JS file name")]
            public string Js { get; set; }

            [Option('y', "ytd", Required = false, HelpText = "print the distance, year-to-date")]
            public bool YearToDate { get; set; }

            [Option("debug", Default = false, Required = false)]
            public bool Debug { get; set;}

        }
    }
}
