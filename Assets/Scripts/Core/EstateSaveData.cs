using System;

namespace UnderstudyKingdom.Core
{
    [Serializable]
    public class EstateSaveData
    {
        /// <summary>
        /// Save-format version, for future migrations. Not yet read by SaveService --
        /// this pass only needs the field to exist so old saves are distinguishable
        /// from a format change later, rather than retrofitting it after players
        /// already have version-less save files.
        /// </summary>
        public int Version = 1;
        public int Coins;
        public LandPlot[] Plots;
    }
}
