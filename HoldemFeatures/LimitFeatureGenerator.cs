using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PokerHandHistory;
using Action = PokerHandHistory.Action;
using System.Diagnostics;
using HoldemHand;

namespace HoldemFeatures
{
    public class LimitFeatureGenerator
    {
		// Map from string value of a feature to its index
		Dictionary<string,Dictionary<string, int>> stringIndexes = new Dictionary<string, Dictionary<string, int>>();

        public bool SkipMissingFeatures { get; set; }
		public bool ConvertFeaturesToNumeric { get; set; }


        public LimitFeatureGenerator()
        {

        }

		public weka.core.Instances GenerateClassifierInstances(int rIdx)
		{
			var atts = generateStateAttributes(rIdx);

			var classVals = new weka.core.FastVector();
			classVals.addElement("Fold");
			classVals.addElement("Call");
			classVals.addElement("Raise");
			atts.addElement(new weka.core.Attribute("Action", classVals));

			var data = new weka.core.Instances(((Rounds)rIdx).ToString().ToLower() + "_data", atts, 0);

			data.setClassIndex(data.numAttributes() - 1);

			return data;
		}

		public weka.core.Instances[] GenerateRegressionInstances(int rIdx)
		{
			var instances = new weka.core.Instances[3];
			string dataName = ((Rounds)rIdx).ToString().ToLower() + "_{0}_data";
			for(int i = 0; i < 3; i++)
			{
				var atts = generateStateAttributes(rIdx);
				string className = i == 0 ? "Fold" : i == 1 ? "Call" : "Raise";
				atts.addElement(new weka.core.Attribute(className));
				var data = new weka.core.Instances(string.Format(dataName, className.ToLower()), atts, 0);

				data.setClassIndex(data.numAttributes() - 1);
				instances[i] = data;
			}
			return instances;
		}

		private weka.core.FastVector generateStateAttributes(int rIdx)
		{
			var atts = new weka.core.FastVector();
			
			// Get all the features of this class.
			foreach (var method in typeof(LimitFeatureGenerator).GetMethods())
			{
				var attributes = method.GetCustomAttributes(typeof(Feature), true);
				if (attributes.Length == 0)
					continue;
				
				// Get the feature attribute on this method.
				var attr = ((Feature)attributes[0]);
				
				if(rIdx < (int)attr.MinRound || rIdx > (int)attr.MaxRound)
					continue;
				
				// Get the name for this column in the CSV file.
				string name = attr.Name;
				
				switch (attr.FType) {
				case FeatureType.Boolean:
				{
					var attVals = new weka.core.FastVector();
					attVals.addElement(Boolean.FalseString);
					attVals.addElement(Boolean.TrueString);
					atts.addElement(new weka.core.Attribute(name, attVals));
				}break;
				case FeatureType.Enum: 
				{
					var attVals = new weka.core.FastVector();
					var vals = Enum.GetNames(attr.EnumType);
					for(int i = 0; i < vals.Length; i++)
						attVals.addElement(vals[i]);
					atts.addElement(new weka.core.Attribute(name, attVals));
				}break;
				case FeatureType.Nominal:
				{
					var attVals = new weka.core.FastVector();
					var vals = attr.NominalValues;
					for(int i = 0; i < vals.Length; i++)
						attVals.addElement(vals[i]);
					atts.addElement(new weka.core.Attribute(name, attVals));
				}break;
				case FeatureType.String:
				{
					atts.addElement(new weka.core.Attribute(name, (weka.core.FastVector)null));
				}break;
				case FeatureType.Discrete:
				case FeatureType.Continuous:
				{
					atts.addElement(new weka.core.Attribute(name));
				}break;
				default: throw new Exception("Unknown attribute type: " + attr.FType.ToString());
				}
			}

			return atts;
		}

		public weka.core.Instances GenerateClassifierFeatures(IEnumerable<PokerHand> hands, Rounds roundFilter = Rounds.NONE)
		{
			var data = GenerateClassifierInstances((int)roundFilter);

			foreach(var hand in hands)
			{
				for (int rIdx = 0; rIdx < hand.Rounds.Length; rIdx++)
				{
					// Optionally filter out rounds
					if (roundFilter != Rounds.NONE && roundFilter != (Rounds)rIdx)
						continue;

					if(hand.Rounds[rIdx] == null || hand.Rounds[rIdx].Actions == null)
						continue;

					for (int aIdx = 0; aIdx < hand.Rounds[rIdx].Actions.Length; aIdx++)
						if (hand.Rounds[rIdx].Actions[aIdx].Player == hand.Hero)
							data.add(GenerateFeatures(hand, rIdx, aIdx, data));
				}
			}

			return data;
		}

		public weka.core.Instances[] GenerateFeatures(IEnumerable<PokerHand> hands, Rounds roundFilter = Rounds.NONE, bool regression = false)
		{
			weka.core.Instances[] data = regression ? GenerateRegressionInstances((int)roundFilter)
													: new weka.core.Instances[] { GenerateClassifierInstances((int)roundFilter) };

			foreach(var hand in hands)
			{
				for (int rIdx = 0; rIdx < hand.Rounds.Length; rIdx++)
				{
					// Optionally filter out rounds
					if (roundFilter != Rounds.NONE && roundFilter != (Rounds)rIdx)
						continue;
					
					if(hand.Rounds[rIdx] == null || hand.Rounds[rIdx].Actions == null)
						continue;
					
					for (int aIdx = 0; aIdx < hand.Rounds[rIdx].Actions.Length; aIdx++)
						if (hand.Rounds[rIdx].Actions[aIdx].Player == hand.Hero)
							for(int dIdx = 0; dIdx < data.Length; dIdx++)
							{
								var inst = GenerateFeatures(hand, rIdx, aIdx, data[dIdx], !regression);
								if(regression)
								{
									switch (hand.Rounds[rIdx].Actions[aIdx].Type) 
									{
										case ActionType.Bet:
										case ActionType.Raise: inst.setClassValue(dIdx == 2 ? 1 : 0);
										break;
										case ActionType.Call:
										case ActionType.Check: inst.setClassValue(dIdx == 1 ? 1 : 0);
										break;
										case ActionType.Fold: inst.setClassValue(dIdx == 0 ? 1 : 0);
										break;
										default:
										break;
									}
								}
								data[dIdx].add(inst);
							}

				}
			}
			
			return data;
		}

		public weka.core.Instance GenerateFeatures(PokerHand hand, int rIdx, int aIdx, weka.core.Instances data, bool generateClass = true)
		{
			// Check that we are using limit betting.
			Debug.Assert(hand.Context.BettingType == BettingType.FixedLimit);
			
			var results = new weka.core.Instance(data.numAttributes());
			results.setDataset(data);
			int attIdx = 0;
			foreach (var method in typeof(LimitFeatureGenerator).GetMethods())
			{
				// Get all the features of this class.
				var attributes = method.GetCustomAttributes(typeof(Feature), true);
				if (attributes.Length == 0)
					continue;
				
				// Get the feature attribute on this method.
				var attr = ((Feature)attributes[0]);
				
				// Get the name for this attribute
				string name = attr.Name;
				//Console.WriteLine("Hand: {0} Feature: {1}", hand.Context.ID, name);
				
				// Get the feature only if it's applicable to this situation.
				object feature = null;
				if(rIdx >= (int)attr.MinRound && rIdx <= (int)attr.MaxRound)
					feature = method.Invoke(this, new object[] { hand, rIdx, aIdx });
				
				if (SkipMissingFeatures && (feature == null || feature.ToString() == "?"))
					continue;

				switch (attr.FType) {
				case FeatureType.Continuous: 
					results.setValue(attIdx, (double)feature);
					break;
				case FeatureType.Discrete: 
					results.setValue(attIdx, (int)feature);
					break;
				case FeatureType.Boolean:
				case FeatureType.Nominal:
				case FeatureType.Enum:
				{
					var attribute = data.attribute(attIdx);
					var attVal = attribute.indexOfValue(feature.ToString());
					if(attVal < 0 || attVal > attribute.numValues())
						throw new Exception(string.Format ("Invalid attribute value: {0} for attribute {1} (idx: {2} total values: {3}", feature.ToString(), name, attVal, attribute.numValues()));
					results.setValue(attribute, attVal);
				}
					break;
				case FeatureType.String:
				{
					var attribute = data.attribute(attIdx);
					results.setValue(attribute, feature.ToString());
				}
					break;
				default: throw new Exception("Unspecified feature type for feature: " + method.Name);
				}

				attIdx++;
			}

			if(generateClass)
			{
				var classAttr = data.classAttribute();
				switch (hand.Rounds[rIdx].Actions[aIdx].Type) 
				{
				case ActionType.Bet:
				case ActionType.Raise: results.setClassValue(classAttr.indexOfValue("Raise"));
					break;
				case ActionType.Call:
				case ActionType.Check: results.setClassValue(classAttr.indexOfValue("Call"));
					break;
				case ActionType.Fold: results.setClassValue(classAttr.indexOfValue("Fold"));;
					break;
				default:
					break;
				}
			}

			return results;
		}

        public Tuple<string,string>[] GenerateFeatures(PokerHand hand, int rIdx, int aIdx)
        {
            // Check that we are using limit betting.
            Debug.Assert(hand.Context.BettingType == BettingType.FixedLimit);

			var results = new List<Tuple<string,string>>();
            foreach (var method in typeof(LimitFeatureGenerator).GetMethods())
            {
				// Get all the features of this class.
                var attributes = method.GetCustomAttributes(typeof(Feature), true);
                if (attributes.Length == 0)
                    continue;
                
                // Get the feature attribute on this method.
                var attr = ((Feature)attributes[0]);
                
                // Get the name for this column in the CSV file.
                string name = attr.Name;
                
                // Get the feature only if it's applicable to this situation.
                string feature = "?";
                if(rIdx >= (int)attr.MinRound && rIdx <= (int)attr.MaxRound)
                    feature = method.Invoke(this, new object[] { hand, rIdx, aIdx }).ToString();

                if (SkipMissingFeatures && feature == "?")
                    continue;

				if(ConvertFeaturesToNumeric)
				{
					switch (attr.FType) {
					case FeatureType.Continuous: 
					case FeatureType.Discrete: break;
					case FeatureType.Nominal: 
						string oldFeature = feature;
						feature = attr.NominalValues.IndexOf(s => s == feature).ToString();
						if(feature == "-1")
							throw new Exception(string.Format("Unknown nominal value {0} for feature {1}", oldFeature, method.Name));
						break;
					case FeatureType.Boolean:
						if(feature == Boolean.FalseString)
							feature = "0";
						else
							feature = "1";
						break;
					case FeatureType.Enum:
						feature = ((int)Enum.Parse(attr.EnumType, feature)).ToString();
						break;
					case FeatureType.String:
						Dictionary<string, int> indexes;
						if(!stringIndexes.TryGetValue(attr.Name, out indexes))
						{
							indexes = new Dictionary<string, int>();
							stringIndexes.Add(attr.Name, indexes);
						}
						int stringIdx;
						if(!indexes.TryGetValue(feature, out stringIdx))
						{
							stringIdx = 1;
							indexes.Add(feature, stringIdx);
						}
						feature = stringIdx.ToString();
						break;
					default: throw new Exception("Unspecified feature type for feature: " + method.Name);
					}
				}

                // Add it to the list of features for this hand.
                results.Add(new Tuple<string, string>(name, feature));
            }

			string actionStr = hand.Rounds[rIdx].Actions[aIdx].Type.ToString();

			switch (hand.Rounds[rIdx].Actions[aIdx].Type) 
			{
			case ActionType.Bet:
			case ActionType.Raise: actionStr = ConvertFeaturesToNumeric ? "2" : "Raise";
			break;
			case ActionType.Call:
			case ActionType.Check: actionStr = ConvertFeaturesToNumeric ? "1" : "Call";
			break;
			case ActionType.Fold: actionStr = ConvertFeaturesToNumeric ? "0" : "Fold";
			break;
			default:
				break;
			}

  			
			results.Add(new Tuple<string, string>("Action", actionStr));
            return results.ToArray();
        }
        
        [Feature("Preflop Bet Level", FeatureType.Discrete)]
        public int PreflopBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Preflop.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.PREFLOP ? actions.Length : aIdx);
        }

		[Feature("Flop Bet Level", FeatureType.Discrete, MinRound=Rounds.FLOP)]
        public int FlopBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Flop.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.FLOP ? actions.Length : aIdx);
        }

		[Feature("Turn Bet Level", FeatureType.Discrete, MinRound=Rounds.TURN)]
        public int TurnBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Turn.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.TURN ? actions.Length : aIdx);
        }

		[Feature("River Bet Level", FeatureType.Discrete, MinRound=Rounds.RIVER)]
        public int RiverBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.River.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.RIVER ? actions.Length : aIdx);
        }

		[Feature("Current Round", FeatureType.Discrete)]
        public int CurrentRound(PokerHand hand, int rIdx, int aIdx)
        {
            return rIdx;
        }

        [Feature("Aggressor Preflop", FeatureType.Boolean, MinRound = Rounds.FLOP)]
        public bool AggressorPreflop(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Preflop.Actions, hand.Hero);
        }

		[Feature("Aggressor Flop", FeatureType.Boolean, MinRound = Rounds.TURN)]
        public bool AggressorFlop(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Flop.Actions, hand.Hero);
        }

		[Feature("Aggressor Turn", FeatureType.Boolean, MinRound = Rounds.RIVER)]
        public bool AggressorTurn(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Turn.Actions, hand.Hero);
        }

		[Feature("Can Check", FeatureType.Boolean, MinRound = Rounds.FLOP)]
        public bool CanCheck(PokerHand hand, int rIdx, int aIdx)
        {
            return hand.Rounds[rIdx].Actions.Take(aIdx).Count(a => a.Type == ActionType.Bet || a.Type == ActionType.Raise) == 0;
        }

		[Feature("Relative Aggressor Position", FeatureType.Nominal, NominalValues=new string[] { "None", "Me", "Before", "After" })]
        public string AggressorPosition(PokerHand hand, int rIdx, int aIdx)
        {
            var action = hand.AllPreviousActions(rIdx, aIdx).LastOrDefault(a => a.Type == ActionType.Bet || a.Type == ActionType.Raise);
            
            // If there has been no raises or bets, no one is the aggressor so we don't use this feature
            if (action == null)
                return "None";

            // If we are the aggressor, return 0
            if (action.Player == hand.Hero)
                return "Me";

            var aggressor = hand.Players.First(p => p.Name == action.Player);
            var relPlayers = hand.ButtonRelativeSeats();
            int aggIdx = relPlayers.IndexOf(p => p.Name == aggressor.Name);
            int heroIdx = relPlayers.IndexOf(p => p.Name == hand.Hero);

            // If the aggressor is after us, return 1.
            // If the aggressor is before us, return -1.
            return aggIdx > heroIdx ? "After" : "Before";
        }

        [Feature("Win Probability", FeatureType.Continuous)]
        public double WinProb(PokerHand hand, int rIdx, int aIdx)
        {
            ulong hc = hand.HeroHoleCards();
            ulong board = hand.BoardMask((Rounds)rIdx);
            int opponents = NumOpponents(hand, rIdx, aIdx);
            return Hand.WinOdds(hc, board, 0UL, opponents, 1000);
        }

        [Feature("Top Pair", FeatureType.Boolean, MinRound = Rounds.FLOP)]
        public bool TopPair(PokerHand hand, int rIdx, int aIdx)
        {
            if(Hand.EvaluateType(hand.HeroHoleCards() | hand.BoardMask((Rounds)rIdx)) != Hand.HandTypes.Pair)
                return false;

            var board = hand.Board((Rounds)rIdx);
            var top = board.Max(c => c.Rank);
            return top == hand.HoleCards[0].Rank || top == hand.HoleCards[1].Rank;            
        }
        
        [Feature("Hand Type", FeatureType.Discrete,  MinRound=Rounds.FLOP)]
        public int HandType(PokerHand hand, int rIdx, int aIdx)
        {
            return (int)Hand.EvaluateType(hand.HeroHoleCards() | hand.BoardMask((Rounds)rIdx));
        }

        [Feature("Board Hand", FeatureType.Discrete, MinRound = Rounds.FLOP)]
        public int BoardHand(PokerHand hand, int rIdx, int aIdx)
        {
            return (int)Hand.EvaluateType(hand.BoardMask((Rounds)rIdx));
        }

        [Feature("Dominant Board Suit", FeatureType.Discrete, MinRound = Rounds.FLOP)]
        public int DominantBoardSuit(PokerHand hand, int rIdx, int aIdx)
        {
            var board = hand.Board((Rounds)rIdx);
            var suits = board.GroupBy(c => c.Suit).OrderByDescending(g => g.Count());

            return suits.First().Count();
        }

        [Feature("Secondary Board Suit", FeatureType.Discrete, MinRound = Rounds.FLOP)]
        public int SecondaryBoardSuit(PokerHand hand, int rIdx, int aIdx)
        {
            var board = hand.Board((Rounds)rIdx);
            var suits = board.GroupBy(c => c.Suit).OrderByDescending(g => g.Count());

            var secondary = suits.Skip(1).FirstOrDefault();
            if (secondary == null)
                return 0;
            return secondary.Count();
        }

        [Feature("Flush Draw Hit", FeatureType.Boolean, MinRound = Rounds.TURN)]
        public bool FlushDrawHit(PokerHand hand, int rIdx, int aIdx)
        {
            var prev = hand.Board((Rounds)(rIdx-1));
            var prevSuits = prev.GroupBy(c => c.Suit).OrderByDescending(g => g.Count());

            if (prevSuits.First().Count() != 2)
                return false;

            var board = hand.Board((Rounds)rIdx);
            var suits = board.GroupBy(c => c.Suit).OrderByDescending(g => g.Count());

            return suits.First().Count() == 3;
        }

        [Feature("Straight Draw Hit", FeatureType.Boolean, MinRound = Rounds.TURN)]
        public bool StraightDrawHit(PokerHand hand, int rIdx, int aIdx)
        {
            var prev = hand.Board((Rounds)(rIdx - 1)).ToMask();
            var prevCount = Hand.CountContiguous(prev);
            if (prevCount != 2)
                return false;

            var board = hand.BoardMask((Rounds)rIdx);
            var hole = hand.HeroHoleCards();

            foreach(var opp in Hand.Hands(0UL, hole | board, 2))
                if (Hand.EvaluateType(board | opp) == Hand.HandTypes.Straight
                    && Hand.EvaluateType(prev | opp) != Hand.HandTypes.Straight
                    && Hand.IsOpenEndedStraightDraw(prev | opp, hole))
                    return true;
            return false;
        }

        [Feature("Four Card Straight Hit", FeatureType.Boolean, MinRound = Rounds.TURN)]
        public bool FourCardStraightHit(PokerHand hand, int rIdx, int aIdx)
        {
            var prev = hand.Board((Rounds)(rIdx - 1)).ToMask();
            int prevCount = Hand.CountContiguous(prev);
            if (prevCount != 3)
                return false;
            return Hand.CountContiguous(hand.BoardMask((Rounds)rIdx)) == 4;
        }

        [Feature("Active Opponents", FeatureType.Discrete)]
        public int NumOpponents(PokerHand hand, int rIdx, int aIdx)
        {
            int folds = 0;
            for (int i = 0; i <= rIdx; i++)
            {
                int stopIdx = i == rIdx ? aIdx : hand.Rounds[i].Actions.Length;
                for (int j = 0; j < stopIdx; j++)
                    if (hand.Rounds[i].Actions[j].Type == ActionType.Fold)
                        folds++;
            }

            return hand.Players.Length - folds - 1;
        }

        [Feature("Table Size", FeatureType.Discrete)]
        public int TableSize(PokerHand hand, int rIdx, int aIdx)
        {
            return hand.Players.Length;
        }

        [Feature("Relative Post-Flop Position", FeatureType.Continuous)]
        public double RelativePosition(PokerHand hand, int rIdx, int aIdx)
        {
            // Get the seat to wrap on
            int maxSeat = hand.Players.Max(p => p.Seat);

            // Figure out who is in first position
            var firstBlind = hand.Blinds.FirstOrDefault(b => b.Type == BlindType.SmallBlind);
            if (firstBlind == null)
                firstBlind = hand.Blinds.FirstOrDefault(b => b.Type == BlindType.BigBlind);
            
            Debug.Assert(firstBlind != null);

            int firstSeat = hand.Players.First(p => p.Name == firstBlind.Player).Seat;
            int heroIdx = hand.HeroSeat();
            
            var actions = hand.AllPreviousActions(rIdx, aIdx);
            
            // Find all players that are acting before this player
            int playersBefore = 0;
            for (int i = firstSeat; i != heroIdx;)
            {
                var player = hand.Players.FirstOrDefault(p => p.Seat == i);
            
                if(player!= null && !actions.Folded(player.Name))
                    playersBefore++;
                
                i++;
                if (i > maxSeat)
                    i = 1;
            }
            
            // Find all players that are acting after this player
            int playersAfter = 0;
            for (int i = heroIdx + 1; i != firstSeat; )
            {
                var player = hand.Players.FirstOrDefault(p => p.Seat == i);

                if (player != null && !actions.Folded(player.Name))
                    playersAfter++;

                i++;
                if (i > maxSeat)
                    i = 1;
            }

            // Return the percentage of players acting before (position relative to players still in)
            return playersBefore / ((double)playersAfter + (double)playersBefore);
        }

        [Feature("Preflop Position", FeatureType.Enum, EnumType=typeof(Position))]
        public Position AbsolutePosition(PokerHand hand, int rIdx, int aIdx)
        {
            // Check if hero is on one of the blinds.
            var blind = hand.Blinds.FirstOrDefault(b => b.Player == hand.Hero && (b.Type == BlindType.BigBlind || b.Type == BlindType.SmallBlind));
            if (blind != null)
                return blind.Type == BlindType.SmallBlind ? Position.SmallBlind : Position.BigBlind;
            
            int heroIdx = hand.Players.First(p => p.Name == hand.Hero).Seat;

            // Immediately check for being the button
            if (hand.Context.Button == heroIdx)
                return Position.Button;

            // Get the seat to wrap on
            int maxSeat = hand.Players.Max(p => p.Seat);
            int minSeat = hand.Players.Min(p => p.Seat);

            string bbPlayer = hand.Blinds.FirstOrDefault(b => b.Type == BlindType.BigBlind).Player;
            int bbSeat = hand.Players.FirstOrDefault(p => p.Name == bbPlayer).Seat;

            int playersBefore = 0;
            for (int i = bbSeat == maxSeat ? minSeat : bbSeat + 1; i != heroIdx; )
            {
                var player = hand.Players.FirstOrDefault(p => p.Seat == i);

                if (player != null)
                    playersBefore++;

                i++;
                if (i > maxSeat)
                    i = minSeat;
            }

            switch (hand.Players.Length)
            {
                case 4: return Position.Early;// only UTG is not a blind or the button
                case 5: return playersBefore > 0 ? Position.Middle : Position.Early; //UTG and UTG+1 to consider
                case 6:
                    {
                        switch (playersBefore)
                        {
                            case 0: return Position.Early;
                            case 1: return Position.Middle;
                            case 2: return Position.Late;
                            default: throw new Exception(string.Format("Impossible players before. Players: {0} Before: {1}", hand.Players.Length, playersBefore));
                        }
                    }
                case 7:
                    {
                        switch (playersBefore)
                        {
                            case 0: 
                            case 1: return Position.Early;
                            case 2: return Position.Middle;
                            case 3: return Position.Late;
                            default: throw new Exception(string.Format("Impossible players before. Players: {0} Before: {1}", hand.Players.Length, playersBefore));
                        }
                    }
                case 8:
                    {
                        switch (playersBefore)
                        {
                            case 0:
                            case 1: return Position.Early;
                            case 2:
                            case 3: return Position.Middle;
                            case 4: return Position.Late;
                            default: throw new Exception(string.Format("Impossible players before. Players: {0} Before: {1}", hand.Players.Length, playersBefore));
                        }
                    }
                case 9:
                    {
                        switch (playersBefore)
                        {
                            case 0:
                            case 1:
                            case 2: return Position.Early;
                            case 3:
                            case 4: return Position.Middle;
                            case 5: return Position.Late;
                            default: throw new Exception(string.Format("Impossible players before. Players: {0} Before: {1}", hand.Players.Length, playersBefore));
                        }
                    }
                case 10:
                    {
                        switch (playersBefore)
                        {
                            case 0:
                            case 1:
                            case 2: return Position.Early;
                            case 3:
                            case 4: return Position.Middle;
                            case 5:
                            case 6: return Position.Late;
                            default: throw new Exception(string.Format("Impossible players before. Players: {0} Before: {1}", hand.Players.Length, playersBefore));
                        }
                    }
                default: throw new Exception("Unsupported player count: " + hand.Players.Length);
            }

        }

        [Feature("Flush Draw", FeatureType.Boolean, MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool FlushDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsFlushDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

		[Feature("Backdoor Flush Draw", FeatureType.Boolean, MinRound = Rounds.FLOP, MaxRound = Rounds.FLOP)]
        public bool BackdoorFlushDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsBackdoorFlushDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

		[Feature("Open-Ended Straight Draw", FeatureType.Boolean, MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool OpenEndedStraightDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsOpenEndedStraightDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

		[Feature("Gutshot Straight Draw", FeatureType.Boolean,  MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool GutshotStraightDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsGutShotStraightDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

		[Feature("Straight Draw Count", FeatureType.Discrete, MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public int StraightDrawCount(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.StraightDrawCount(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

		[Feature("Flush Draw Count", FeatureType.Discrete, MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public int FlushDrawCount(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.FlushDrawCount(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }
        
		[Feature("Holecards Suited", FeatureType.Boolean)]
        public bool Suited(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsSuited(hand.HeroHoleCards());
        }

        [Feature("Holecards Connected", FeatureType.Boolean)]
        public bool Connectors(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsConnected(hand.HeroHoleCards());
        }

		[Feature("Pocket Pair", FeatureType.Boolean)]
        public bool PocketPair(PokerHand hand, int rIdx, int aIdx)
        {
            return hand.HoleCards[0].Rank == hand.HoleCards[1].Rank;
        }

		[Feature("Holecard 1 Rank", FeatureType.Discrete)]
        public int Holecard1Rank(PokerHand hand, int rIdx, int aIdx)
        {
            return Math.Max((int)hand.HoleCards[0].Rank, (int)hand.HoleCards[1].Rank);
        }

		[Feature("Holecard 2 Rank", FeatureType.Discrete)]
        public int Holecard2Rank(PokerHand hand, int rIdx, int aIdx)
        {
            return Math.Min((int)hand.HoleCards[0].Rank, (int)hand.HoleCards[1].Rank);
        }

		[Feature("Sklansky Group", FeatureType.Discrete)]
        public int SklanskyGroup(PokerHand hand, int rIdx, int aIdx)
        {
            return (int)PocketHands.GroupType(hand.HeroHoleCards());
        }

		[Feature("Positive Potential", FeatureType.Continuous)]
        public double PositivePotential(PokerHand hand, int rIdx, int aIdx)
        {
            double ppot, npot;
            Hand.HandPotential(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), out ppot, out npot, NumOpponents(hand, rIdx, aIdx), 1000);
            return ppot;
        }

		[Feature("Negative Potential", FeatureType.Continuous)]
        public double NegativePotential(PokerHand hand, int rIdx, int aIdx)
        {
            double ppot, npot;
            Hand.HandPotential(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), out ppot, out npot, NumOpponents(hand, rIdx, aIdx), 1000);
            return npot;
        }

		[Feature("Hand Strength", FeatureType.Continuous)]
        public double HandStrength(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.HandStrength(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), NumOpponents(hand, rIdx, aIdx), 1000);
        }

        // Determines if the hero was the last person to bet in a round
        private bool getAggressor(Action[] actions, string hero)
        {
            bool agg = false;
            foreach (var action in actions)
                if (action.Type == ActionType.Bet || action.Type == ActionType.Raise)
                    agg = action.Player == hero;
            return agg;
        }

        // Gets the number of total bets put in by the highest raisor.
        private int getBetLevel(Action[] actions, int aIdx)
        {
			if(actions == null)
				return 0;
            int level = 0;
            for (int i = 0; i < actions.Length && i < aIdx; i++)
                if (actions[i].Type == ActionType.Bet || actions[i].Type == ActionType.Raise)
                    level++;
            return level;
        }

        
    }
}
