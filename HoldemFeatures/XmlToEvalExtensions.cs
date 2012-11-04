using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PokerHandHistory;
using HoldemHand;

namespace HoldemFeatures
{
    public static class XmlToEvalExtensions
    {
        public static ulong HeroHoleCards(this PokerHand hand)
        {
            return hand.HoleCards.ToMask();
        }

        #region Convert XML cards to ulong bitmasks
        public static ulong ToMask(this IEnumerable<PokerHandHistory.Card> cards)
        {
            ulong result = 0UL;
            foreach (var c in cards)
                result |= c.ToMask();
            return result;
        }

        public static ulong ToMask(this PokerHandHistory.Card card)
        {
            switch (card.Suit)
            {
                case Suit.Clubs: return Hand.Mask(Hand.CLUB_OFFSET + (int)card.Rank - 1);
                case Suit.Diamonds: return Hand.Mask(Hand.DIAMOND_OFFSET + (int)card.Rank - 1);
                case Suit.Hearts: return Hand.Mask(Hand.HEART_OFFSET + (int)card.Rank - 1);
                case Suit.Spades: return Hand.Mask(Hand.SPADE_OFFSET + (int)card.Rank - 1);
                default:
                    throw new Exception("Unknown Card Type: " + card.ToString());
            }
        }
        #endregion

        #region Convert board cards to ulong masks
        /// <summary>
        /// Converts the board cards to a ulong mask for analysis.
        /// </summary>
        /// <param name="hand">The hand to convert.</param>
        /// <returns>A ulong mask for use in analysis.</returns>
        public static ulong BoardMask(this PokerHand hand)
        {
            return BoardMask(hand, (Rounds)hand.Rounds.Length - 1);
        }

        /// <summary>
        /// Converts the board cards to a ulong mask for analysis.
        /// </summary>
        /// <param name="hand">The hand to convert.</param>
        /// <param name="round">The maximum round to include in the board mask.</param>
        /// <returns>A ulong mask for use in analysis.</returns>
        public static ulong BoardMask(this PokerHand hand, Rounds round)
        {
            ulong result = 0UL;
            for (int i = 1; i <= (int)round; i++)
                result |= hand.Rounds[i].CommunityCards.ToMask();
            return result;
        }

        public static PokerHandHistory.Card[] Board(this PokerHand hand, Rounds round)
        {
            switch (round)
            {
                case Rounds.PREFLOP: return new PokerHandHistory.Card[0];
                case Rounds.FLOP: return new PokerHandHistory.Card[] { hand.Flop.CommunityCards[0], hand.Flop.CommunityCards[1], hand.Flop.CommunityCards[2] };
                case Rounds.TURN: return new PokerHandHistory.Card[] { hand.Flop.CommunityCards[0], hand.Flop.CommunityCards[1], hand.Flop.CommunityCards[2], hand.Turn.CommunityCards[0] };
                case Rounds.RIVER: return new PokerHandHistory.Card[] { hand.Flop.CommunityCards[0], hand.Flop.CommunityCards[1], hand.Flop.CommunityCards[2], hand.Turn.CommunityCards[0], hand.River.CommunityCards[0] };
                default: throw new Exception("Unknown round: " + round.ToString());
            }

        }

        #endregion

        /// <summary>
        /// Gets all previous actions before the given (round, action)
        /// </summary>
        /// <param name="hand">The hand to collect actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the returned list.</param>
        /// <returns>A list of all previous actions.</returns>
        public static IEnumerable<PokerHandHistory.Action> AllPreviousActions(this PokerHand hand, int rIdx, int aIdx)
        {
            List<PokerHandHistory.Action> actions = new List<PokerHandHistory.Action>();

            // All previous rounds.
            for (int i = 0; i < rIdx; i++)
                actions.AddRange(hand.Rounds[i].Actions);

            // All previous actions in current round.
            for (int i = 0; i < aIdx; i++)
                actions.Add(hand.Rounds[rIdx].Actions[i]);

            return actions;
        }


        /// <summary>
        /// Tells whether a given player has folded at this point in the game.
        /// </summary>
        /// <param name="hand">The hand to check actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the check.</param>
        /// <param name="playerName">The player to check for folding.</param>
        /// <returns>True if the player has folded.</returns>
        public static bool Folded(this PokerHand hand, int rIdx, int aIdx, string playerName)
        {
            return hand.AllPreviousActions(rIdx, aIdx).FirstOrDefault(a => a.Type == ActionType.Fold && a.Player == playerName) != null;
        }

        /// <summary>
        /// Tells whether a given player has gone all-in at this point in the game.
        /// </summary>
        /// <param name="hand">The hand to check actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the check.</param>
        /// <param name="playerName">The player to check for going all-in.</param>
        /// <returns>True if the player has gone all-in.</returns>
        public static bool AllIn(this PokerHand hand, int rIdx, int aIdx, string playerName)
        {
            return hand.AllPreviousActions(rIdx, aIdx).FirstOrDefault(a => a.AllIn && a.Player == playerName) != null;
        }

        /// <summary>
        /// Tells whether a given player has folded at this point in the game.
        /// </summary>
        /// <param name="hand">The hand to check actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the check.</param>
        /// <param name="playerName">The player to check for folding.</param>
        /// <returns>True if the player has folded.</returns>
        public static bool Folded(this IEnumerable<PokerHandHistory.Action> actions, string playerName)
        {
            return actions.FirstOrDefault(a => a.Type == ActionType.Fold && a.Player == playerName) != null;
        }

        /// <summary>
        /// Tells whether a given player has gone all-in at this point in the game.
        /// </summary>
        /// <param name="hand">The hand to check actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the check.</param>
        /// <param name="playerName">The player to check for going all-in.</param>
        /// <returns>True if the player has gone all-in.</returns>
        public static bool AllIn(this IEnumerable<PokerHandHistory.Action> actions, string playerName)
        {
            return actions.FirstOrDefault(a => a.AllIn && a.Player == playerName) != null;
        }

        public static int HeroSeat(this PokerHand hand)
        {
            return hand.Players.First(p => p.Name == hand.Hero).Seat;
        }

        public static decimal HeroStack(this PokerHand hand)
        {
            return hand.Players.First(p => p.Name == hand.Hero).Stack;
        }

        /// <summary>
        /// Creates a list of the hand's players ordered by their seat relative to the button.
        /// 0 = immediately after the button
        /// N = button, where N is the number of players
        /// </summary>
        public static IEnumerable<Player> ButtonRelativeSeats(this PokerHand hand)
        {
            Player[] players = new Player[hand.Players.Length];
            int maxSeat = hand.Players.Max(p => p.Seat);
            int minSeat = hand.Players.Min(p => p.Seat);
            for (int i = hand.Context.Button == maxSeat ? minSeat : hand.Context.Button + 1, relIdx = 0; relIdx < players.Length;)
            {
                var player = hand.Players.FirstOrDefault(p => p.Seat == i);
                if (player != null)
                    players[relIdx++] = player;

                i++;
                if (i > maxSeat)
                    i = minSeat;
            }
            return players;
        }
    }
}
