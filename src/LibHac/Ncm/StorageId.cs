using System;
using System.Collections.Generic;
using System.Text;

namespace LibHac.Ncm
{
    public enum StorageId : byte
    {
        None = 0,
        Host = 1,
        GameCard = 2,
        BuiltInSystem = 3,
        BuiltInUser = 4,
        SdCard = 5
    }
}
