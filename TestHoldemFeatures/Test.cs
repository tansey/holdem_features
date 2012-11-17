using System;
using NUnit.Framework;
using HoldemFeatures;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace TestHoldemFeatures
{
	[TestFixture()]
	public class Test
	{
		const string utg = @"<?xml version=""1.0"" encoding=""utf-16""?>
<PokerHand xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
		<Blinds Player=""Dave_Wilkes"" Type=""SmallBlind"" Amount=""10"" />
			<Blinds Player=""Some_Killa"" Type=""BigBlind"" Amount=""20"" />
				<HoleCards Rank=""Jack"" Suit=""Clubs"" />
				<HoleCards Rank=""Eight"" Suit=""Clubs"" />
				<Rounds />
				<Context Online=""false"" Site=""SimulatedPokerSite"" Currency=""$"" ID=""0"" TimeStamp=""2012-11-15T09:39:39.769352-06:00"" Format=""CashGame"" Button=""1"" BigBlind=""20"" SmallBlind=""10"" BettingType=""FixedLimit"" PokerVariant=""TexasHoldEm"" />
				<Players Name=""TeeJayorTJ5"" Stack=""1000"" Seat=""1"" />
				<Players Name=""Dave_Wilkes"" Stack=""1000"" Seat=""2"" />
				<Players Name=""Some_Killa"" Stack=""1000"" Seat=""3"" />
				<Players Name=""Better_Boy"" Stack=""1000"" Seat=""4"" />
				<Players Name=""Kiddo1973"" Stack=""1000"" Seat=""5"" />
				<Players Name=""Human"" Stack=""1000"" Seat=""6"" />
				<Rake>0</Rake>
				<Hero>Better_Boy</Hero>
				</PokerHand>";

		[Test()]
		public void UtgAction ()
		{
			LimitFeatureGenerator _featureGen = new LimitFeatureGenerator() { SkipMissingFeatures = true };

			var hand = buildHand(utg);

			var data = _featureGen.GenerateClassifierInstances(0);

			_featureGen.GenerateFeatures(hand, 0, 0, data, false);
		}

		private PokerHandHistory.PokerHand buildHand(string handStr)
		{
			using(TextReader reader = new StringReader(handStr))
			{
				XmlSerializer ser = new XmlSerializer(typeof(PokerHandHistory.PokerHand));
				return (PokerHandHistory.PokerHand)ser.Deserialize(reader);
			}
		}
	}
}

