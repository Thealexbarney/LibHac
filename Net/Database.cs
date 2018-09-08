using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using libhac;
using Newtonsoft.Json;

namespace Net
{
    public class Database
    {
        public Dictionary<long, TitleMetadata> Titles { get; set; } = new Dictionary<long, TitleMetadata>();
        public DateTime VersionListTime { get; set; }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static Database Deserialize(string filename)
        {
            var text = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject<Database>(text);
        }

        public bool IsVersionListCurrent()
        {
            return VersionListTime.AddDays(1) > DateTime.UtcNow;
        }

        public void ImportVersionList(VersionList list)
        {
            foreach (var title in list.titles)
            {
                var mainId = long.Parse(title.id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                long updateId = 0;
                bool isUpdate = (mainId & 0x800) != 0;
                if (isUpdate)
                {
                    updateId = mainId;
                    mainId &= ~0x800;
                }

                if (!Titles.TryGetValue(mainId, out TitleMetadata titleDb))
                {
                    titleDb = new TitleMetadata();
                    Titles[mainId] = titleDb;
                }

                titleDb.Id = mainId;
                titleDb.UpdateId = updateId;
                titleDb.MaxVersion = title.version;

                int maxVersionShort = title.version >> 16;
                for (int i = 0; i <= maxVersionShort; i++)
                {
                    var version = i << 16;

                    if (!titleDb.Versions.TryGetValue(version, out TitleVersion versionDb))
                    {
                        versionDb = new TitleVersion { Version = version };
                        titleDb.Versions.Add(version, versionDb);
                    }
                }
            }
        }

        public void ImportList(string filename)
        {
            ImportList(File.ReadAllLines(filename));
        }

        public void ImportList(string[] titleIds)
        {
            foreach (string id in titleIds)
            {
                var mainId = long.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                long updateId = 0;
                bool isUpdate = (mainId & 0x800) != 0;
                if (isUpdate)
                {
                    updateId = mainId;
                    mainId &= ~0x800;
                }

                var titleDb = new TitleMetadata();
                Titles[mainId] = titleDb;

                titleDb.Id = mainId;
                titleDb.UpdateId = mainId | 0x800;
                titleDb.MaxVersion = 5 << 16;

                int maxVersionShort = 5;
                for (int i = 0; i <= maxVersionShort; i++)
                {
                    var version = i << 16;

                    if (!titleDb.Versions.TryGetValue(version, out TitleVersion versionDb))
                    {
                        versionDb = new TitleVersion { Version = version };
                        titleDb.Versions.Add(version, versionDb);
                    }
                }
            }
        }
    }

    public class TitleMetadata
    {
        public long Id { get; set; }
        public long UpdateId { get; set; }
        public List<long> AocIds { get; set; } = new List<long>();
        public int MaxVersion { get; set; }
        public Dictionary<int, TitleVersion> Versions { get; set; } = new Dictionary<int, TitleVersion>();


    }

    public class TitleVersion
    {
        public bool Exists { get; set; } = true;
        public int Version { get; set; }
        public Cnmt ContentMetadata { get; set; }
        public Nacp Control { get; set; }
    }
}
