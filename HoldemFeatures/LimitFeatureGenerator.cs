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
    public class LimitFeatureGenerator : IFeatureGenerator
    {
        private class SeatInfo
        {
            public int Index { get; set; }
            public int BetLevel { get; set; }
        }

        public LimitFeatureGenerator()
        {

        }

        public Tuple<string, string>[] GenerateFeatures(PokerHand hand, int rIdx, int aIdx)
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
                string feature = "";
                if(rIdx >= (int)attr.MinRound && rIdx <= (int)attr.MaxRound)
                    feature = method.Invoke(this, new object[] { hand, rIdx, aIdx }).ToString();

                // Add it to the list of features for this hand.
                results.Add(new Tuple<string, string>(name, feature));
            }
            return results.ToArray();
        }
        
        [Feature("Preflop Bet Level")]
        public int PreflopBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Preflop.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.PREFLOP ? actions.Length : aIdx);
        }

        [Feature("Flop Bet Level", MinRound=Rounds.FLOP)]
        public int FlopBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Flop.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.FLOP ? actions.Length : aIdx);
        }

        [Feature("Turn Bet Level", MinRound=Rounds.TURN)]
        public int TurnBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.Turn.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.TURN ? actions.Length : aIdx);
        }

        [Feature("River Bet Level", MinRound=Rounds.RIVER)]
        public int RiverBetLevel(PokerHand hand, int rIdx, int aIdx)
        {
            var actions = hand.River.Actions;
            return getBetLevel(actions, rIdx > (int)Rounds.RIVER ? actions.Length : aIdx);
        }

        [Feature("Current Round")]
        public int CurrentRound(PokerHand hand, int rIdx, int aIdx)
        {
            return rIdx;
        }

        [Feature("Aggressor Preflop", MinRound = Rounds.FLOP)]
        public bool AggressorPreflop(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Preflop.Actions, hand.Hero);
        }

        [Feature("Aggressor Flop", MinRound = Rounds.TURN)]
        public bool AggressorFlop(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Flop.Actions, hand.Hero);
        }

        [Feature("Aggressor Turn", MinRound = Rounds.RIVER)]
        public bool AggressorTurn(PokerHand hand, int rIdx, int aIdx)
        {
            return getAggressor(hand.Turn.Actions, hand.Hero);
        }

        [Feature("Win Probability")]
        public double WinProb(PokerHand hand, int rIdx, int aIdx)
        {
            //TODO: Use the cached version instead of this
            ulong hc = hand.HeroHoleCards();
            ulong board = hand.BoardMask((Rounds)rIdx);
            int opponents = NumOpponents(hand, rIdx, aIdx);
            return Hand.WinOdds(hc, board, 0UL, opponents, 0.01, 100);
        }

        [Feature("Opponent count")]
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

        [Feature("Relative Post-Flop Position")]
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
            int heroIdx = hand.Players.First(p => p.Name == hand.Hero).Seat;
            
            var actions = hand.PreviousActions(rIdx, aIdx);
            
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

        [Feature("Flush Draw", MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool FlushDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsFlushDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

        [Feature("Open-Ended Straight Draw", MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool OpenEndedStraightDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsOpenEndedStraightDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
        }

        [Feature("Gutshot Straight Draw",  MinRound = Rounds.FLOP, MaxRound = Rounds.TURN)]
        public bool GutshotStraightDraw(PokerHand hand, int rIdx, int aIdx)
        {
            return Hand.IsGutShotStraightDraw(hand.HeroHoleCards(), hand.BoardMask((Rounds)rIdx), 0UL);
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
            int level = 0;
            for (int i = 0; i < actions.Length && i < aIdx; i++)
                if (actions[i].Type == ActionType.Bet || actions[i].Type == ActionType.Raise)
                    level++;
            return level;
        }

        
    }
}
