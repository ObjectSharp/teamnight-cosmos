using System;
using System.Collections.Generic;
using System.Text;

namespace CosmosSpeedTestApi
{
    public class SpeedTestOptions
    {
        public string CosmosUri { get; set; }
        public string CosmosKey { get; set; }
        public IEnumerable<string> PreferredLocations { get; set; }
    }
}
