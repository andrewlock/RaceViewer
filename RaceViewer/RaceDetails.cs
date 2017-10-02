namespace RaceViewer
{
    public class RaceDetails
    {
        public string Name { get; set; }
        public string Start { get; set; }
        public string NormalisedStart => Start?.Trim()?.ToLower()?.Replace(" ", "");

        public string Medal { get; set; }
        public string Dates { get; set; }
        public string Website { get; set; }
        public string Cost { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
