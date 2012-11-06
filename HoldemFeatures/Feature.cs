using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HoldemFeatures
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class Feature : Attribute
    {
		/// <summary>
		/// The name of this feature.
		/// </summary> 
        public readonly string Name;

		/// <summary>
		/// The type of the feature.
		/// </summary>
		public readonly FeatureType FType;

        public Feature(string name, FeatureType featureType)
        {
            Name = name;
			FType = featureType;
            MinRound = Rounds.PREFLOP;
            MaxRound = Rounds.RIVER;
        }

        public Rounds MinRound { get; set; }
        public Rounds MaxRound { get; set; }
		public string[] NominalValues { get; set; }
		public Type EnumType { get; set; }
    }
}
