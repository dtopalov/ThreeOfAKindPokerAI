namespace TexasHoldem.AI.ThreeOfAKind.Helpers
{
    using System.Collections.Generic;
    using System.Linq;

    using Logic;
    using Logic.Cards;
    using Logic.Helpers;

    public static class Analyzers
    {
        private static readonly HandEvaluator HandEvaluator = new HandEvaluator();

        public static int CountOuts(ICollection<Card> currentHand, HandRankType target)
        {
            var outCount = 0;
            foreach (var card in Deck.AllCards)
            {
                if (currentHand.Contains(card))
                {
                    continue;
                }

                currentHand.Add(card);

                if (HandEvaluator.GetBestHand(currentHand).RankType >= target)
                {
                    outCount++;
                }

                currentHand.Remove(card);
            }

            return outCount;
        }

        public static int HasFlushChance(IEnumerable<Card> cards)
        {
            var suitedCards = cards.Select(c => c.Suit)
                                   .GroupBy(c => c)
                                   .OrderByDescending(g => g)
                                   .FirstOrDefault()
                                   .Count();

            switch (suitedCards)
            {
                case 4:
                    return 3;
                case 3:
                    return 2;
                case 2:
                    return 1;
                default:
                    return 0;
            }
        }

        public static int HasStraightChance(ICollection<Card> cards)
        {
            var sortedCards = cards.Select(c => (int)c.Type)
                 .OrderByDescending(t => t)
                 .ToList();

            if (sortedCards.Contains(14))
            {
                sortedCards.Add(1);
            }

            var holesCount = 0;
            var notConnectedCards = 0;

            for (var i = 1; i < sortedCards.Count; i++)
            {
                if (sortedCards[i] - sortedCards[i - 1] < 3)
                {
                    holesCount += sortedCards[i - 1] - sortedCards[i] + 1;
                }
                else
                {
                    notConnectedCards++;
                }

                if (holesCount > 2)
                {
                    holesCount -= 2;
                    notConnectedCards++;
                }
            }

            // 4 - straight in community cards - handle it elsewhere
            // 3 - very big chance; 2 - likely to have, 1 - possible if very lucky, 0 or less - no possible - i think :)
            return cards.Count - holesCount - notConnectedCards - 1;
        }
    }
}
