using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PokerHandHistory;
using System.Xml.Serialization;

namespace HoldemFeatures
{
    class Program
    {
        static string DELIMITER = ",";
        static IFeatureGenerator featureGen = new LimitFeatureGenerator();
        
        static void Main(string[] args)
        {
            #region Validate parameters
            if (args.Length < 2)
            {
                Console.WriteLine("Format: HoldemFeatures.exe <input> <output> [-option1 ...]");
                return;
            }

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i][0] != '-')
                    continue;

                string flag = args[i].Substring(1);
                switch (flag)
                {
                    case "delim":
                    case "d":
                    case "delimiter": DELIMITER = args[++i];
                        string delim = DELIMITER.ToLower();
                        if (delim == "tab" || delim == "tabs" || delim == "\\t")
                            DELIMITER = "\t";
                        break;
                    default:
                        Console.WriteLine("Unknown flag: {0}", flag);
                        return;
                }
            }

            string inFilename = args[0];
            if (!File.Exists(inFilename))
            {
                Console.WriteLine("No input file found at {0}", inFilename);
                return;
            }
            #endregion

            #region Read in all hands
            Console.Write("Loading hand histories... ");
            PokerHandXML hands;
            using (TextReader reader = new StreamReader(inFilename))
            {
                XmlSerializer ser = new XmlSerializer(typeof(PokerHandXML));
                hands = (PokerHandXML)ser.Deserialize(reader);
            }
            Console.WriteLine("done.");
            Console.WriteLine("Loaded {0} hand{1}.", hands.Hands.Length, hands.Hands.Length == 1 ? "" : "s");
            #endregion

            #region Iterate over every hero action and generate features
            Console.WriteLine("Generating features...");
            var features = new List<Tuple<string,string>[]>();
            for (int hIdx = 0; hIdx < hands.Hands.Length; hIdx++)
            {
                if (hIdx % 1000 == 0)
                    Console.WriteLine("Hand: {0}", hIdx);
                
                var hand = hands.Hands[hIdx];
                
                for (int rIdx = 0; rIdx < hand.Rounds.Length; rIdx++)
                {
                    if (hand.Rounds[rIdx].Actions == null)
                        continue;
                    for (int aIdx = 0; aIdx < hand.Rounds[rIdx].Actions.Length; aIdx++)
                        if (hand.Rounds[rIdx].Actions[aIdx].Player == hand.Hero)
                            features.Add(featureGen.GenerateFeatures(hand, rIdx, aIdx));
                }
            }
            Console.WriteLine("done.");
            Console.WriteLine("Generated {0} features for {1} decision{2}", features.First().Count(), features.Count, features.Count == 1 ? "" : "s");
            #endregion

            #region Write the results to a delimited file
            Console.Write("Writing results to file... ");
            using (TextWriter writer = new StreamWriter(args[1]))
            {
                if(features.Count == 0)
                    return;
                writer.WriteLine(features[0].Select(t => t.Item1).Flatten(DELIMITER));
                foreach (var row in features)
                    writer.WriteLine(row.Select(t => t.Item2).Flatten(DELIMITER));
            }
            Console.WriteLine("done.");
            #endregion
        }
    }
}
