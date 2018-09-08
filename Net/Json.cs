﻿// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Net
{
    public static class Json
    {
        public static VersionList ReadVersionList(string filename)
        {
            var text = File.ReadAllText(filename);
            var versionList = JsonConvert.DeserializeObject<VersionList>(text);
            return versionList;
        }
    }

    public class VersionList
    {
        public List<VersionListTitle> titles { get; set; }
        public int format_version { get; set; }
        public long last_modified { get; set; }
    }

    public class VersionListTitle
    {
        public string id { get; set; }
        public int version { get; set; }
        public int required_version { get; set; }
    }
}
