using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        [JsonProperty("pointer")]
        public int CachePointer { get; set; }
    }

    public class NetEaseBaseModel
    {
        [JsonProperty("id")]
        public uint Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class NetEaseTrackModel : NetEaseBaseModel
    {
        public class NetEaseTrackAlbumModel : NetEaseBaseModel
        {
            [JsonProperty("picUrl")]
            public string Cover { get; set; }
        }

        public class NetEaseTrackArtists : NetEaseBaseModel
        {

        }

        [JsonProperty("album")]
        public NetEaseTrackAlbumModel Album { get; set; }

        [JsonProperty("artists")]
        public List<NetEaseTrackArtists> Artists { get; set; }

        /// <summary>
        /// Duration in milliseconds
        /// </summary>
        [JsonProperty("duration")]
        public uint Duration { get; set; }
    }

    public class NetEaseSoundModel
    {
        [JsonProperty("track")]
        public NetEaseTrackModel Track { get; set; }

        [JsonProperty("tid")]
        public uint Id { get; set; }
    }

    public static class NetEaseCacheManager
    {
        private static string[] Files = { "queue", "queue_backup", "history" };

        public static NetEaseSoundModel GetSoundInfo(int tid)
        {
            foreach (var f in Files)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetEase", "CloudMusic", "webdata", "file", f);
                if (!File.Exists(path))
                {
                    continue;
                }
                var data = File.ReadAllText(path, Encoding.UTF8);
                var json = JsonConvert.DeserializeObject<NetEaseSoundModel[]>(data);
                var find = json.SingleOrDefault(x => x.Id == tid);
                if (find != null)
                {
                    return find;
                }
            }

            return null;
        }
    }
}
