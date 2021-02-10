using CommandLine;
using Geo.Gps;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocationHistoryConvertor
{
    public class Options
    {
        public enum Split
        {
            None,
            Year,
            Month
        }

        [Option('i', "input", Required = true, HelpText = "Set source file")]
        public string Source { get; set; }

        [Option('o', "output", Required = true, HelpText = "Set output file")]
        public string Output { get; set; }

        [Option('s', "split", Required = true, HelpText = "Split by")]
        public Split SplitBy { get; set; }
    }

    public class GooglePartialConvert
    {
        public DateTime Time;
        public GoogleLocationData Data;
    }

    public class GoogleLocationExport
    {
        public List<GoogleLocationData> locations;
    }

    public class GoogleLocationData
    {
        public string timestampMs;
        public int latitudeE7;
        public int longitudeE7;
        public int accuracy;
        public int velocity;
        public int altitude;
        public int verticalAccuracy;
    }



    class Program
    {

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        private static long GroupYear(DateTime dateTime) => dateTime.Year;
        private static long None(DateTime dateTime) => 0;

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(o =>
                  {
                      Console.WriteLine($"Reading source {o.Source}");
                      using StreamReader sw = new StreamReader(o.Source);
                      using JsonReader reader = new JsonTextReader(sw);
                      var serializer = new JsonSerializer();
                      var json = serializer.Deserialize<GoogleLocationExport>(reader);


                      Func<GooglePartialConvert, string> groupBy = o.SplitBy switch
                      {
                          Options.Split.None => (GooglePartialConvert data) => "",
                          Options.Split.Year => (GooglePartialConvert data) => data.Time.Year.ToString(),
                          Options.Split.Month => (GooglePartialConvert data) => $"{data.Time.Year}-{data.Time.Month}",
                          _ => throw new NotImplementedException(),
                      };

                      Console.WriteLine($"Parsing source {o.Source}");

                      var groups = json.locations.Select(x => new GooglePartialConvert
                      {
                          Time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(x.timestampMs)).DateTime,
                          Data = x
                      }).GroupBy(groupBy);

                      foreach (var group in groups)
                      {
                          var track = new Track();
                          var trackSegment = new TrackSegment();

                          foreach (var data in group)
                          {
                              var location = data.Data;
                              trackSegment.Waypoints.Add(new Waypoint(location.latitudeE7 / 1e7, location.longitudeE7 / 1e7, location.altitude, data.Time));
                          }

                          track.Segments.Add(trackSegment);


                          var gpsData = new GpsData();
                          gpsData.Tracks.Add(track);

                          Console.WriteLine($"Found {trackSegment.Waypoints.Count} waypoints");
                          var output = Path.Combine(Path.GetDirectoryName(o.Output), $"{Path.GetFileName(o.Output)}_{group.Key}.gpx");
                          Console.WriteLine($"Writting output {output}");

                          var gpx11Serializer = new Geo.Gps.Serialization.Gpx11Serializer();
                          using var writeStream = File.OpenWrite(output);
                          gpx11Serializer.Serialize(gpsData, writeStream);
                      }

                  });
        }
    }
}
