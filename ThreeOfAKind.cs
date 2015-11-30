namespace TexasHoldem.AI.ThreeOfAKind
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Logic;
    using Logic.Cards;
    using Logic.Helpers;
    using Logic.Players;

    using Stages;

    // TODO: HQC
    public class ThreeOfAKind : BasePlayer
    {
        private static bool isSmallBlind;
        private static int outs;
        private readonly IHandEvaluator handEvaluator = new HandEvaluator();
        private HandRankType currentHandRank;
        private ICollection<Card> hand;
        private ICollection<PlayerActionType> opponentActions = new List<PlayerActionType>();

        private static bool flag;

        private static bool smallBlindFlag;

        private static bool isCallingStation;

        private static bool isVeryAggressive;

        public override string Name { get; } = "___" + Guid.NewGuid();

        public override PlayerAction GetTurn(GetTurnContext context)
        {
            if (context.RoundType == GameRoundType.PreFlop && !smallBlindFlag)
            {
                smallBlindFlag = true;
                isSmallBlind = true;
            }

            isSmallBlind = context.MyMoneyInTheRound == context.SmallBlind;

            // antiCrash prefix - do not delete
            if (context.MoneyLeft == 0)
            {
                return PlayerAction.CheckOrCall();
            }

            if (context.PreviousRoundActions.Any() && context.SmallBlind <= 2 && context.RoundType != GameRoundType.PreFlop)
            {
                this.opponentActions.Add(context.PreviousRoundActions.Last().Action.Type);
            }

            if (this.opponentActions.Any() && !flag && context.SmallBlind == 5)
            {
                flag = true;
                isCallingStation = this.FindCallingStation(this.opponentActions) &&
                    context.PreviousRoundActions.Any() && 
                    !context.PreviousRoundActions.Last().PlayerName.Contains("Smokin");

                isVeryAggressive = this.FindAggressiveStation(this.opponentActions);
            }

            // get current Rank
            if (context.RoundType != GameRoundType.PreFlop)
            {
                this.currentHandRank = this.GetCurrentHandRank();
            }

            // fishing prefix - nais :)
            if (/*context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                && context.MoneyToCall > 0
                &&*/ context.MoneyToCall < 12)
            {
                if (context.RoundType != GameRoundType.River)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(context.MoneyLeft);
                }
            }

            // TODO: Some better way to access stages
            if (context.RoundType == GameRoundType.PreFlop)
            {
                return this.PreflopLogic(context);
            }

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

        private HandRankType GetCurrentHandRank()
        {
            this.hand = this.CommunityCards.ToList();
            this.hand.Add(this.FirstCard);
            this.hand.Add(this.SecondCard);

            return this.handEvaluator.GetBestHand(this.hand).RankType;
        }

        private PlayerAction RiverLogic(GetTurnContext context)
        {
            this.currentHandRank = this.handEvaluator.GetBestHand(this.hand).RankType;

            if (isCallingStation && this.currentHandRank > HandRankType.Pair &&
                context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
            {
                return PlayerAction.Raise(context.CurrentPot * 2);
            }

            //if (isVeryAggressive && context.PreviousRoundActions.Any() && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
            //{
            //    if (this.FirstCard.Type >= CardType.King || this.SecondCard.Type >= CardType.King)
            //    {
            //        return PlayerAction.CheckOrCall();
            //    }

            //    if (this.currentHandRank >= HandRankType.Straight)
            //    {
            //        return PlayerAction.Raise(context.MoneyLeft);
            //    }
            //}

            if (isSmallBlind && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
            {
                return PlayerAction.Raise((context.CurrentPot / 2) + 1);
            }

            // TODO: add handrank
            if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
            {
                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(context.CurrentPot);
                }

                if (this.currentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (!isCallingStation && context.MoneyToCall <= context.CurrentPot/2 && this.currentHandRank >= HandRankType.Pair)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Fold();
            }

            if (this.currentHandRank >= HandRankType.TwoPairs)
            {
                return PlayerAction.Raise(context.CurrentPot);
            }

            return PlayerAction.CheckOrCall();
        }

        private bool FindCallingStation(ICollection<PlayerActionType> actions)
        {
            int calls = actions.Count(x => x == PlayerActionType.CheckCall);
            if ((calls * 100 / actions.Count) > 65)
            {
                return true;
            }

            return false;
        }

        private bool FindAggressiveStation(ICollection<PlayerActionType> actions)
        {
            int raises = actions.Count(x => x == PlayerActionType.Raise);
            if (raises * 100 / actions.Count > 50)
            {
                return true;
            }

            return false;
        }

        private PlayerAction TurnLogic(GetTurnContext context)
        {
            if (isCallingStation && this.currentHandRank > HandRankType.Pair)
            {
                return PlayerAction.Raise(context.CurrentPot * 2);
            }

            if (this.currentHandRank >= HandRankType.TwoPairs)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot);
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(context.MoneyLeft);
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.currentHandRank < HandRankType.Straight)
            {
                outs = this.CountOuts(this.hand, HandRankType.Straight);
            }

            if (outs > 11)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
                {
                    if (context.MoneyToCall * 100 / context.CurrentPot < outs * 2)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                return PlayerAction.Raise((context.CurrentPot * 2) / 3);
            }

            //if (isVeryAggressive)
            //{
            //    if (outs > 11)
            //    {
            //        return PlayerAction.Raise(context.CurrentPot * 2);
            //    }

            //    if (this.currentHandRank >= HandRankType.Pair)
            //    {
            //        return PlayerAction.CheckOrCall();
            //    }
            //}

            if (outs < 8)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
                {
                    return PlayerAction.Fold();
                }
            }

            return PlayerAction.CheckOrCall();
        }

        private PlayerAction FlopLogic(GetTurnContext context)
        {
            if (isCallingStation && this.currentHandRank > HandRankType.Pair)
            {
                return PlayerAction.Raise(context.CurrentPot * 2);
            }

            if (this.currentHandRank > HandRankType.Pair)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot);
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(context.CurrentPot * 2);
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.FirstCard.Type == this.SecondCard.Type)
            {
                outs = this.CountOuts(this.hand, HandRankType.ThreeOfAKind);
            }
            else if (this.currentHandRank < HandRankType.Straight)
            {
                outs = this.CountOuts(this.hand, HandRankType.Straight);
            }

            //if (isVeryAggressive && context.PreviousRoundActions.Any() 
            //    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
            //{
            //    if (outs >= 7)
            //    {
            //        return PlayerAction.Raise(context.MoneyLeft);
            //    }
            //}

            if (outs > 11)
            {
                return PlayerAction.Raise(context.CurrentPot * 2);
            }

            if (outs > 8)
            {
                if (context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Raise(context.CurrentPot);
            }

            if (outs < 6)
            {
                if (context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.Fold();
                }

                return PlayerAction.CheckOrCall();
            }

            return PlayerAction.CheckOrCall();
        }

        private PlayerAction PreflopLogic(GetTurnContext context)
        {
            // to rework ... or not
            var playHand = PreflopHandStrengthValuation.GetRecommendation(this.FirstCard, this.SecondCard);

            if (context.MoneyLeft <= context.SmallBlind * 10 /*&& playHand > CardValuationType.Unplayable*/)
            {
                return PlayerAction.Raise(context.MoneyLeft);
            }

            if (context.PreviousRoundActions.Count == 2)
            {
                if (playHand == CardValuationType.Unplayable)
                {
                    return PlayerAction.Fold();
                }

                if (playHand == CardValuationType.Monster)
                {
                    return PlayerAction.Raise(context.CurrentPot * 10);
                }

                if (playHand >= CardValuationType.Recommended)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.Raise(16 * context.SmallBlind);
                    }

                    return PlayerAction.Raise(8 * context.SmallBlind);
                }

                if (playHand == CardValuationType.Risky)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Raise(6 * context.SmallBlind);
                }

                if (playHand == CardValuationType.NotRecommended)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.CheckOrCall();
                    }

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
                    if (this.FirstCard.Type == CardType.Ace || this.SecondCard.Type == CardType.Ace)
                    {
                        return PlayerAction.Raise(context.MoneyLeft);
                    }

                    return PlayerAction.Fold();
                }

                if (playHand == CardValuationType.Monster)
                {
                    return PlayerAction.Raise(Math.Min((2 * context.MoneyToCall) + context.CurrentPot, context.MoneyLeft));
                }

                if (playHand >= CardValuationType.Recommended)
                {
                    return PlayerAction.CheckOrCall();
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

            if (!isSmallBlind && context.PreviousRoundActions.Count >= 3)
            {
                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
                {
                    if (playHand == CardValuationType.NotRecommended)
                    {
                        return PlayerAction.Fold();
                    }
                }

                if (context.PreviousRoundActions.Count > 3)
                {
                    if (playHand == CardValuationType.Risky)
                    {
                        if (context.MoneyToCall > context.MoneyLeft * 0.2)
                        {
                            return PlayerAction.Fold();
                        }
                    }

                    return PlayerAction.CheckOrCall();
                }

                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.CheckCall)
                {
                    if (playHand == CardValuationType.Monster)
                    {
                        return PlayerAction.Raise(context.CurrentPot * 5);
                    }

                    if (playHand >= CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise(8 * context.SmallBlind);
                    }

                    if (playHand == CardValuationType.Risky)
                    {
                        return PlayerAction.Raise(6 * context.SmallBlind);
                    }

                    if (playHand == CardValuationType.NotRecommended)
                    {
                        return PlayerAction.CheckOrCall();
                    }
                }

                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                    && playHand != CardValuationType.NotRecommended)
                {
                    if (playHand > CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise(Math.Min(context.CurrentPot + (2 * context.MoneyToCall), context.MoneyLeft));
                    }

                    if (playHand == CardValuationType.Recommended)
                    {
                        if (context.MoneyToCall < context.MoneyLeft)
                        {
                            return PlayerAction.CheckOrCall();
                        }

                        return PlayerAction.Fold();
                    }

                    if (playHand == CardValuationType.Risky)
                    {
                        if (context.MoneyToCall > context.MoneyLeft * 0.2)
                        {
                            return PlayerAction.Fold();
                        }

                        return PlayerAction.CheckOrCall();
                    }
                }
            }

            return PlayerAction.CheckOrCall();
        }

        private int CountOuts(ICollection<Card> currentHand, HandRankType target)
        {
            var outCount = 0;
            foreach (var card in Deck.AllCards)
            {
                if (currentHand.Contains(card))
                {
                    continue;
                }

                currentHand.Add(card);

                if (this.handEvaluator.GetBestHand(currentHand).RankType >= target)
                {
                    outCount++;
                }

                currentHand.Remove(card);
            }

            return outCount;
        }
    }
}