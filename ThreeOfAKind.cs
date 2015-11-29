namespace TexasHoldem.AI.ThreeOfAKind
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Logic;
    using Logic.Cards;
    using Logic.Extensions;
    using Logic.Helpers;
    using Logic.Players;

    using Stages;

    // TODO: HQC
    public class ThreeOfAKind : BasePlayer
    {
        private static bool isSmallBlind;
        private IHandEvaluator handEvaluator = new HandEvaluator();

        public override string Name { get; } = "3ofAKind" + Guid.NewGuid();

        public override PlayerAction GetTurn(GetTurnContext context)
        {
            // TODO: Some better way to access stages
            if (context.RoundType == GameRoundType.PreFlop)
            {
                return this.PreflopLogic(context);
            }

            if (context.RoundType == GameRoundType.Flop)
            {
                return this.FlopLogic(context);
            }

            return PlayerAction.CheckOrCall();

            if (context.RoundType == GameRoundType.Turn)
            {
                return this.TurnLogic(context);
            }

            if (context.RoundType == GameRoundType.River)
            {
                return this.RiverLogic(context);
            }

            return PlayerAction.CheckOrCall();
        }

        private PlayerAction RiverLogic(GetTurnContext context)
        {
            throw new NotImplementedException();
        }

        private PlayerAction TurnLogic(GetTurnContext context)
        {
            throw new NotImplementedException();
        }

        private PlayerAction FlopLogic(GetTurnContext context)
        {
            var hand = this.CommunityCards.ToList();
            hand.Add(this.FirstCard);
            hand.Add(this.SecondCard);

            var currentHandRank = this.handEvaluator.GetBestHand(hand).RankType;

            int outs = 0;
            if ((int)currentHandRank < 3500)
            {
                outs = this.CountOuts(hand);
            }

            if (outs > 10)
            {
                return PlayerAction.Raise(context.MoneyLeft);
            }

            return PlayerAction.CheckOrCall();
        }

        private PlayerAction PreflopLogic(GetTurnContext context)
        {
            if (context.MoneyLeft == 0)
            {
                return PlayerAction.CheckOrCall();
            }

            // to rework ... or not
            var playHand = PreflopHandStrengthValuation.GetRecommendation(this.FirstCard, this.SecondCard);

            if (context.MoneyLeft <= context.SmallBlind * 10)
            {
                return PlayerAction.Raise(context.MoneyLeft);
            }

            if (context.PreviousRoundActions.Count == 2)
            {
                isSmallBlind = true;
                if (playHand == CardValuationType.Unplayable)
                {
                    return PlayerAction.Fold();
                }

                if (playHand == CardValuationType.Recommended)
                {
                    return PlayerAction.Raise(8 * context.SmallBlind);
                }

                if (playHand == CardValuationType.Risky)
                {
                    return PlayerAction.Raise(6 * context.SmallBlind);
                }

                if (playHand == CardValuationType.NotRecommended)
                {
                    return PlayerAction.Raise(4 * context.SmallBlind);
                }
            }

            // Facing a raise
            if (isSmallBlind && context.PreviousRoundActions.Count > 2)
            {
                if (playHand == CardValuationType.NotRecommended)
                {
                    return PlayerAction.Fold();
                }

                if (context.MoneyLeft <= context.SmallBlind * 20)
                {
                    return PlayerAction.Raise(context.MoneyLeft);
                }

                if (playHand == CardValuationType.Recommended)
                {
                    return PlayerAction.Raise(Math.Min((2 * context.MoneyToCall) + context.CurrentPot, context.MoneyLeft));
                }

                if (isSmallBlind && context.MoneyToCall <= context.MoneyLeft * 0.2)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (isSmallBlind && context.MoneyToCall > context.MoneyLeft * 0.2)
                {
                    return PlayerAction.Fold();
                }
            }

            return PlayerAction.CheckOrCall();
        }

        private int CountOuts(ICollection<Card> hand)
        {
            var outs = 0;
            foreach (var card in Deck.AllCards)
            {
                if (hand.Contains(card))
                {
                    continue;
                }

                hand.Add(card);

                if ((int)this.handEvaluator.GetBestHand(hand).RankType > 3500)
                {
                    outs++;
                }

                hand.Remove(card);
            }

            return outs;
        }
    }
}
