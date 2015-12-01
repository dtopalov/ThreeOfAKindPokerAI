namespace TexasHoldem.AI.ThreeOfAKind
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Helpers;

    using Logic;
    using Logic.Cards;
    using Logic.Helpers;
    using Logic.Players;

    using Stages;

    // TODO: HQC
    public class ThreeOfAKind : BasePlayer
    {
        private const int MagicFishingNumber = 12;
        private const int MagicNumber = 3;

        private static bool isSmallBlind;
        private static int outs;

        private static bool flag;

        private static bool smallBlindFlag;

        private static bool isCallingStation;

        private static bool isVeryAggressive;

        private readonly IHandEvaluator handEvaluator = new HandEvaluator();
        private CardValuationType ownCardsStrength = 0;
        private HandRankType currentHandRank;
        private ICollection<Card> hand;
        private ICollection<PlayerActionType> opponentActions = new List<PlayerActionType>();

        public override string Name { get; } = "AlwaysCallDummyPlayer_" + Guid.NewGuid();

        public override PlayerAction GetTurn(GetTurnContext context)
        {
            if (context.RoundType == GameRoundType.PreFlop && !smallBlindFlag)
            {
                smallBlindFlag = true;
                isSmallBlind = true;
            }

            isSmallBlind = context.MyMoneyInTheRound == context.SmallBlind;

            // antiCrash prefix - do not delete
            if (context.MoneyLeft <= 0)
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
                    !context.PreviousRoundActions.Last().PlayerName.Contains("mokin");

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
                &&*/ context.MoneyToCall < MagicFishingNumber && context.RoundType != GameRoundType.PreFlop)
            {
                if (context.RoundType != GameRoundType.River)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (this.currentHandRank >= HandRankType.Straight && this.ImproveHand())
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }

                if (this.ownCardsStrength >= CardValuationType.Strong && Analyzers.CountOuts(this.hand, HandRankType.Straight) < 4 && this.handEvaluator.GetBestHand(this.CommunityCards).RankType < HandRankType.Pair && this.currentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.Raise((context.CurrentPot * 3) + MagicNumber);
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

        private static int AllIn(int moneyLeft)
        {
            return moneyLeft - MagicNumber > MagicNumber
                ? moneyLeft - MagicNumber
                : moneyLeft;
        }

        private bool ImproveHand()
        {
            return this.handEvaluator.GetBestHand(this.hand)
                    .CompareTo(this.handEvaluator.GetBestHand(this.CommunityCards)) > 0;
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
                return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
            }

            if (isVeryAggressive && context.PreviousRoundActions.Any() &&
                context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                && context.MoneyToCall <= ((context.CurrentPot / 2) + 1)
                && !context.PreviousRoundActions.Last().PlayerName.Contains("ColdCall"))
            {
                if (this.FirstCard.Type >= CardType.Queen || this.SecondCard.Type >= CardType.Queen)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }
            }

            if (context.CanCheck && !isCallingStation)
            {
                return PlayerAction.Raise((context.CurrentPot / 2) + MagicNumber);
            }

            // TODO: add handrank
            if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
            {
                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.currentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (!isCallingStation && context.MoneyToCall <= context.CurrentPot / 2 && this.currentHandRank >= HandRankType.Pair)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Fold();
            }

            if (this.currentHandRank >= HandRankType.TwoPairs)
            {
                return PlayerAction.Raise(context.CurrentPot + MagicNumber);
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
            if (raises * 100 / actions.Count > 70)
            {
                return true;
            }

            return false;
        }

        private PlayerAction TurnLogic(GetTurnContext context)
        {
            if (isCallingStation && this.currentHandRank > HandRankType.Pair)
            {
                return PlayerAction.Raise((context.CurrentPot * 2) + MagicNumber);
            }

            if (this.currentHandRank >= HandRankType.TwoPairs)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.currentHandRank < HandRankType.Straight)
            {
                outs = Analyzers.CountOuts(this.hand, HandRankType.Straight);
            }

            if (outs > 11)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
                {
                    if (context.MoneyToCall * 100 / context.CurrentPot < outs * 2)
                    {
                        if (isVeryAggressive && !context.PreviousRoundActions.Last().PlayerName.Contains("ColdCall"))
                        {
                            return PlayerAction.Raise(AllIn(context.MoneyLeft));
                        }

                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                return PlayerAction.Raise(((context.CurrentPot * 2) / 3) + MagicNumber);
            }

            if (!context.PreviousRoundActions.Last().PlayerName.Contains("ColdCall") && isVeryAggressive && this.currentHandRank == HandRankType.Pair &&
                context.MoneyToCall <= context.CurrentPot * 2 / 3)
            {
                return PlayerAction.CheckOrCall();
            }

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
                return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
            }

            if (isVeryAggressive)
            {
                if (this.currentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.Raise((context.MoneyToCall * 2) + MagicNumber);
                }

                if (context.MoneyToCall <= context.CurrentPot && this.currentHandRank >= HandRankType.Pair)
                {
                    return PlayerAction.CheckOrCall();
                }
            }

            if (this.currentHandRank > HandRankType.TwoPairs)
            {
                if (context.PreviousRoundActions.Any()
                    && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.currentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.FirstCard.Type == this.SecondCard.Type)
            {
                outs = Analyzers.CountOuts(this.hand, HandRankType.ThreeOfAKind);
            }
            else if (this.currentHandRank < HandRankType.Straight)
            {
                outs = Analyzers.CountOuts(this.hand, HandRankType.Straight);
            }

            if (isVeryAggressive && context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                && !context.PreviousRoundActions.Last().PlayerName.Contains("ColdCall"))
            {
                if (outs >= 8)
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }
            }

            if (outs > 11)
            {
                return PlayerAction.Raise(context.CurrentPot + MagicNumber);
            }

            if (outs > 8)
            {
                if (context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type != PlayerActionType.Raise
                && context.MoneyToCall * 100 / context.CurrentPot < outs * 5)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Raise((context.CurrentPot * 2 / 3) - MagicNumber);
            }

            if (outs < 6)
            {
                if (context.PreviousRoundActions.Any()
                && context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
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
            this.ownCardsStrength = PreflopHandStrengthValuation.GetRecommendation(this.FirstCard, this.SecondCard);

            if (context.MoneyLeft <= context.SmallBlind * 10 /*&& ownCardsStrength > CardValuationType.Unplayable*/)
            {
                return PlayerAction.Raise(context.MoneyLeft);
            }

            if (context.PreviousRoundActions.Count == 2)
            {
                if (this.ownCardsStrength == CardValuationType.Unplayable)
                {
                    return PlayerAction.Fold();
                }

                if (this.ownCardsStrength == CardValuationType.Monster)
                {
                    return PlayerAction.Raise((context.CurrentPot * 10) - MagicNumber);
                }

                if (this.ownCardsStrength >= CardValuationType.Recommended)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.Raise((16 * context.SmallBlind) - MagicNumber);
                    }

                    return PlayerAction.Raise((8 * context.SmallBlind) - MagicNumber);
                }

                if (this.ownCardsStrength == CardValuationType.Risky)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Raise((6 * context.SmallBlind) - MagicNumber);
                }

                if (this.ownCardsStrength == CardValuationType.NotRecommended)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Raise((4 * context.SmallBlind) + MagicNumber);
                }
            }

            // Facing a raise
            if (isSmallBlind && context.PreviousRoundActions.Count > 2)
            {
                if (this.ownCardsStrength == CardValuationType.NotRecommended)
                {
                    return PlayerAction.Fold();
                }

                if (context.MoneyLeft <= context.SmallBlind * 20)
                {
                    if (this.FirstCard.Type == CardType.Ace || this.SecondCard.Type == CardType.Ace)
                    {
                        return PlayerAction.Raise(AllIn(context.MoneyLeft));
                    }

                    return PlayerAction.Fold();
                }

                if (this.ownCardsStrength == CardValuationType.Monster)
                {
                    return PlayerAction.Raise(Math.Min(((2 * context.MoneyToCall) + MagicNumber) + context.CurrentPot, AllIn(context.MoneyLeft)));
                }

                if (this.ownCardsStrength >= CardValuationType.Recommended)
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
                    if (this.ownCardsStrength == CardValuationType.NotRecommended)
                    {
                        return PlayerAction.Fold();
                    }
                }

                if (context.PreviousRoundActions.Count > 3)
                {
                    if (this.ownCardsStrength == CardValuationType.Risky)
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
                    if (this.ownCardsStrength == CardValuationType.Monster)
                    {
                        return PlayerAction.Raise((context.CurrentPot * 5) - MagicNumber);
                    }

                    if (this.ownCardsStrength >= CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise((8 * context.SmallBlind) - MagicNumber);
                    }

                    if (this.ownCardsStrength == CardValuationType.Risky)
                    {
                        return PlayerAction.Raise((6 * context.SmallBlind) - MagicNumber);
                    }

                    if (this.ownCardsStrength == CardValuationType.NotRecommended)
                    {
                        return PlayerAction.CheckOrCall();
                    }
                }

                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                    && this.ownCardsStrength != CardValuationType.NotRecommended)
                {
                    if (this.ownCardsStrength > CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise(Math.Min(context.CurrentPot + (2 * context.MoneyToCall), context.MoneyLeft));
                    }

                    if (this.ownCardsStrength == CardValuationType.Recommended)
                    {
                        if (context.MoneyToCall < context.MoneyLeft)
                        {
                            return PlayerAction.CheckOrCall();
                        }

                        return PlayerAction.Fold();
                    }

                    if (this.ownCardsStrength == CardValuationType.Risky)
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
    }
}