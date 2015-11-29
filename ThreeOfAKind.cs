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

    using TexasHoldem.AI.SmartPlayer;

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

            return PlayerAction.CheckOrCall();

            if (context.RoundType == GameRoundType.Flop)
            {
                return this.FlopLogic(context);
            }

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
            var outs = this.CountOuts(this.FirstCard, this.SecondCard, this.CommunityCards);
            throw new NotImplementedException();
        }

        private PlayerAction FlopLogic(GetTurnContext context)
        {
            throw new NotImplementedException();
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

        private int CountOuts(Card firstCard, Card secondCard, IReadOnlyCollection<Card> communityCards)
        {
            var hand = communityCards.ToList();
            hand.Add(firstCard);
            hand.Add(secondCard);

            HandRankType currentHandRank = this.handEvaluator.GetBestHand(hand).RankType;

            return 0;
        }
    }
}
