using System;

namespace UnderstudyKingdom.Core
{
    [Serializable]
    public class EstateSaveData
    {
        public int Version = 1;
        public int Coins;
        public LandPlot[] Plots;
    }
}
