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
        static LimitFeatureGenerator featureGen = new LimitFeatureGenerator();
        static Rounds ROUND_FILTER = Rounds.NONE;
		static string FILE_FORMAT = "arff";
		static bool REGRESSION = false;

        static void Main(string[] args)
        {
//			Console.WriteLine("**** WARNING: HARD CODED PARAMETERS IN USE ****");
//			args= @"/Users/wesley/Dropbox/Public/hands.xml /Users/wesley/poker/classifiers/datasets/river.arff -round river -regression".Split();

            #region Validate parameters
            if (args.Length < 2)
            {
                Console.WriteLine("Format: HoldemFeatures.exe <input> <output> [-option1 ...]");
                Console.WriteLine("Options:");

                // Delimiter
                Console.WriteLine("[d, delim, delimiter] <arg1>".PadRight(30) + "Sets the delimiter character for the output file.");
                Console.WriteLine("".PadRight(30) + "Setting arg1 to tab or \t will set the delimiter as tabs.");
                Console.WriteLine("".PadRight(30) + "Default: ,");

                // Round
                Console.WriteLine("[r, round] <arg1>".PadRight(30) + "Filters the actions to only those in the given round.");
                Console.WriteLine("".PadRight(30) + "Options: preflop, flop, turn, river, none");
                Console.WriteLine("".PadRight(30) + "Default: none (all rounds)");

				// Convert features to numeric values
				Console.WriteLine("[num, numeric]".PadRight(30) + "Automatically converts all features to numeric values.");
				Console.WriteLine("Default: disabled.".PadLeft(30));

				// Convert features to numeric values
				Console.WriteLine("[format] <arg1>".PadRight(30) + "Sets the file format to save to.");
				Console.WriteLine("".PadRight(30) + "Options: arff, csv");
				Console.WriteLine("Default: arff.".PadLeft(30));

				Console.WriteLine("[regress, regression]".PadRight(30) + "Generates datasets for regression rather than classification.");
				Console.WriteLine("Default: disabled (classification)".PadLeft(30));
                return;
            }

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i][0] != '-')
                    continue;

                string flag = args[i].Substring(1).ToLower();
                switch (flag)
                {
                    case "delim":
                    case "d":
                    case "delimiter": DELIMITER = args[++i];
                        string delim = DELIMITER.ToLower();
                        if (delim == "tab" || delim == "tabs" || delim == "\\t")
                            DELIMITER = "\t";
                        break;
                    case "round":
                    case "r": ROUND_FILTER = (Rounds)Enum.Parse(typeof(Rounds), args[++i], true);
                        featureGen.SkipMissingFeatures = true;
                        break;
					case "num":
					case "numeric": featureGen.ConvertFeaturesToNumeric = true;
						break;
					case "format": FILE_FORMAT = args[++i].ToLower();
					if(FILE_FORMAT != "csv" && FILE_FORMAT != "arff")
					{
						Console.WriteLine("Unknown file format: {0}. Options are csv or arff", FILE_FORMAT);
						return;
					}
					break;
					case "regression":
					case "regress": REGRESSION = true;
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
			weka.core.Instances[] features = featureGen.GenerateFeatures(hands.Hands, ROUND_FILTER, REGRESSION);
            Console.WriteLine("done.");
            Console.WriteLine("Generated {0} features for {1} decision{2}", features[0].numAttributes(), features[0].numInstances(), features[0].numInstances() == 1 ? "" : "s");
            #endregion

            #region Write the results to a delimited file
            Console.Write("Writing results to file... ");
			if(REGRESSION)
			{
				string filename = args[1].Insert(args[1].IndexOf('.'), "_{0}");
				writeFile(features[0], string.Format(filename, "fold"));
				writeFile(features[1], string.Format(filename, "call"));
				writeFile(features[2], string.Format(filename, "raise"));
			}
			else
				writeFile(features[0], args[1]);
   			Console.WriteLine("done.");
            #endregion
        }

		private static void writeFile(weka.core.Instances features, string filename)
		{
			if(FILE_FORMAT == "csv")
			{
				using (TextWriter writer = new StreamWriter(filename))
				{
					if(features.numInstances() == 0)
						return;
					for(int i = 0; i < features.numAttributes(); i++)
					{
						writer.Write(features.attribute(i));
						if(i < features.numAttributes() - 1)
							writer.Write(DELIMITER);
					}
					writer.WriteLine();
					for(int i = 0; i < features.numInstances(); i++)
					{
						for(int j = 0; j < features.numAttributes(); j++)
						{
							writer.Write(features.instance(i).value(j));
							if(j < features.numAttributes() - 1)
								writer.Write(DELIMITER);
						}
						writer.WriteLine();
					}
				}
			} else if (FILE_FORMAT == "arff")
			{
				using (TextWriter writer = new StreamWriter(filename))
				{
					writer.WriteLine(features.toString());
				}
			}
		}
    }
}
