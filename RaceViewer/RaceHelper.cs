using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;

namespace RaceViewer
{
    public class RaceHelper
    {
        private readonly string _excelFile;
        const string _jsonFile = "races.json";
        private readonly HttpClient _httpClient;
        private readonly IHostingEnvironment _env;
        private readonly UrlEncoder _urlEncoder;

        public object _lock = new object();
        private List<RaceDetails> _races;

        public RaceHelper(IHostingEnvironment env, UrlEncoder urlEncoder, IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://maps.googleapis.com");
            _env = env;
            _urlEncoder = urlEncoder;
            _excelFile = configuration["RotaFileName"] ?? "Rota.xlsx";
        }

        public List<RaceDetails> GetRaces()
        {
            if (_races != null) { return _races; }
            lock (_lock)
            {
                if (_races != null) { return _races; }

                var newRaces = ReadRaces();
                var existing = LoadRacesFromJson();
                var combined = MergeRaceDetails(newRaces, existing).GetAwaiter().GetResult();
                SaveDetailsToFile(combined);
                _races = combined;
            }
            return _races;
        }

        private List<RaceDetails> ReadRaces()
        {
            var races = new List<RaceDetails>();
            var fileInfo = new FileInfo(_excelFile);
            using (ExcelPackage package = new ExcelPackage(fileInfo))
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Races");

                if (worksheet == null) { return new List<RaceDetails>(); }

                for (var i = worksheet.Dimension.Start.Row + 1; i <= worksheet.Dimension.End.Row; i++)
                {
                    var race = new RaceDetails
                    {
                        Name = worksheet.Cells[i, 4].Value?.ToString(),
                        Start = worksheet.Cells[i, 9].Value?.ToString(),
                        Medal = worksheet.Cells[i, 5].Value?.ToString(),
                        Cost = worksheet.Cells[i, 6].Value?.ToString(),
                        Website = worksheet.Cells[i, 11].Value?.ToString(),
                        Dates = FirstNonNull(
                            worksheet.Cells[i, 3].Value?.ToString(),
                            worksheet.Cells[i, 2].Value?.ToString(),
                            worksheet.Cells[i, 1].Value?.ToString()
                            ),
                    };
                    if(string.IsNullOrEmpty(race.Name) || string.IsNullOrEmpty(race.Start))
                    {
                        //you get extra dummy lines depending on how they're defined in the spreadsheet
                        continue;
                    }
                    races.Add(race);
                }
            }
            return races;
        }

        private static string FirstNonNull(params string[] vals)
        {
            return vals.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? string.Empty;
        }

        private List<RaceDetails> LoadRacesFromJson()
        {
            var file = _env.ContentRootFileProvider.GetFileInfo(_jsonFile);
            if (!file.Exists) return new List<RaceDetails>();

            var text = File.ReadAllText(file.PhysicalPath);
            return JsonConvert.DeserializeObject<List<RaceDetails>>(text);
        }

        private async Task<List<RaceDetails>> MergeRaceDetails(List<RaceDetails> allRaces, List<RaceDetails> knownRaces)
        {
            var updated = new List<RaceDetails>();
            foreach (var race in allRaces)
            {
                var known = knownRaces.FirstOrDefault(x => x.NormalisedStart == race.NormalisedStart);
                if (known != null)
                {
                    race.Latitude = known.Latitude;
                    race.Longitude = known.Longitude;
                    updated.Add(race);
                    continue;
                }

                //need to get post code
                (var ok, var lat, var lng) = await GetDetailsFromSummary(race);
                if (ok)
                {
                    race.Latitude = lat;
                    race.Longitude = lng;
                    updated.Add(race);
                }
            }
            return updated;
        }

        private void SaveDetailsToFile(List<RaceDetails> races)
        {
            var file = _env.ContentRootFileProvider.GetFileInfo(_jsonFile);
            var text = JsonConvert.SerializeObject(races);

            File.WriteAllText(file.PhysicalPath, text);
        }

        private async Task<(bool ok, decimal? lat, decimal? lng)> GetDetailsFromSummary(RaceDetails summary)
        {
            try
            {
                var postCode = _urlEncoder.Encode(summary.Start);
                var text = await _httpClient.GetStringAsync("/maps/api/geocode/json?address=" + postCode);

                var response = JObject.Parse(text);

                var results = (JArray)response["results"];
                var result = results.FirstOrDefault();
                var geo = result?["geometry"]?["location"];

                var latVal = geo?["lat"]?.ToString();
                var lngVal = geo?["lng"]?.ToString();

                decimal? lat = null;
                decimal? lng = null;

                if (!string.IsNullOrEmpty(latVal) && decimal.TryParse(latVal, out var value))
                {
                    lat = value;
                }
                if (!string.IsNullOrEmpty(lngVal) && decimal.TryParse(lngVal, out var value2))
                {
                    lng = value2;
                }

                return (lat.HasValue && lng.HasValue, lat, lng);
            }
            catch (Exception)
            {
                return (false, null, null);
            }
        }
    }
}
