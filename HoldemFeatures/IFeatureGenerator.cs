using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PokerHandHistory;

namespace HoldemFeatures
{
    public interface IFeatureGenerator
    {
        Tuple<string, string>[] GenerateFeatures(PokerHand hand, int rIdx, int aIdx);
    }
}
