using Newtonsoft.Json;
using System;

namespace CosmosSpeedTestApi.Models
{
    public class Pixel
    {
        public string id { get; set; }

        public string name { get; set; }

        public int r { get; set; }

        public int g { get; set; }

        public int b { get; set; }

        public int a { get; set; }

        public int x { get; set; }

        public int y { get; set; }

        public int seq { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}
