using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HoldemFeatures
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class Feature : Attribute
    {
        public readonly string Name;

        public Feature(string name)
        {
            Name = name;
            MinRound = Rounds.PREFLOP;
            MaxRound = Rounds.RIVER;
        }

        public Rounds MinRound { get; set; }
        public Rounds MaxRound { get; set; }
    }
}
