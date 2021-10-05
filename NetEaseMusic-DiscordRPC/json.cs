using Newtonsoft.Json;

namespace NetEaseMusic_DiscordRPC
{
    public class MemoryOffset
    {
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("offsets")]
        public Offset Offsets { get; set; }
    }

    public struct Offset
    {
        [JsonProperty("length")]
        public int Length { get; set; }
        [JsonProperty("schedule")]
        public int Schedule { get; set; }
    }

}
