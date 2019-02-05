using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LibHac;
using Newtonsoft.Json;

namespace Net
{
    public class Database
    {
        public Dictionary<ulong, TitleMetadata> Titles { get; set; } = new Dictionary<ulong, TitleMetadata>();
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
            foreach (VersionListTitle title in list.titles)
            {
                ulong mainId = ulong.Parse(title.id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                AddTitle(mainId);
            }
        }

        public void AddTitle(ulong id, int version = -1)
        {
            bool isUpdate = (id & 0x800) != 0;

            if (!Titles.TryGetValue(id, out TitleMetadata titleDb))
            {
                titleDb = new TitleMetadata { Id = id };
                Titles[id] = titleDb;
            }

            if (version >= 0)
            {
                titleDb.MaxVersion = version;

                int minVersion = isUpdate ? 1 : 0;

                int maxVersionShort = titleDb.MaxVersion >> 16;
                for (int i = minVersion; i <= maxVersionShort; i++)
                {
                    int longVersion = i << 16;

                    if (!titleDb.Versions.TryGetValue(longVersion, out TitleVersion versionDb))
                    {
                        versionDb = new TitleVersion { Version = longVersion };
                        titleDb.Versions.Add(longVersion, versionDb);
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
                ulong mainId = ulong.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                AddTitle(mainId);
            }
        }
    }

    public class TitleMetadata
    {
        public ulong Id { get; set; }
        public int MaxVersion { get; set; }
        public List<SuperflyInfo> Superfly { get; set; } = new List<SuperflyInfo>();
        public DateTime SuperflyTime { get; set; }
        public Dictionary<int, TitleVersion> Versions { get; set; } = new Dictionary<int, TitleVersion>();

        public bool IsSuperflyCurrent() => SuperflyTime.AddDays(15) > DateTime.UtcNow;
    }

    public class TitleVersion
    {
        public bool Exists { get; set; } = true;
        public int Version { get; set; }
        public Cnmt ContentMetadata { get; set; }
        public Nacp Control { get; set; }
    }
}
