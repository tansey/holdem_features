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

        #endregion

        /// <summary>
        /// Gets all previous actions before the given (round, action)
        /// </summary>
        /// <param name="hand">The hand to collect actions.</param>
        /// <param name="rIdx">The index of the current round.</param>
        /// <param name="aIdx">The index of the current action. This action will not be included in the returned list.</param>
        /// <returns>A list of all previous actions.</returns>
        public static IEnumerable<PokerHandHistory.Action> PreviousActions(this PokerHand hand, int rIdx, int aIdx)
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
            return hand.PreviousActions(rIdx, aIdx).FirstOrDefault(a => a.Type == ActionType.Fold && a.Player == playerName) != null;
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
            return hand.PreviousActions(rIdx, aIdx).FirstOrDefault(a => a.AllIn && a.Player == playerName) != null;
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
    }
}
