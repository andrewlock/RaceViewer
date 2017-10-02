using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace RaceViewer
{
    public class RacesController
    {
        private readonly RaceHelper _races;

        public RacesController(RaceHelper races)
        {
            _races = races;
        }

        [HttpGet("/races")]
        public IEnumerable<RaceDetails> Races()
        {
            return _races.GetRaces();
        }
    }
}
