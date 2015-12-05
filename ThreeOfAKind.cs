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

    // TODO: HQC
    public class ThreeOfAKind : BasePlayer
    {
        private const int MagicFishingNumber = 12;
        private const int MagicNumber = 3;
        private PlayerActionType? lastAction;
        private static bool isSmallBlind;
        private static int outs;

        private static bool flag;

        private static bool smallBlindFlag;

        private static bool isCallingStation;

        private static bool isVeryAggressive;

        private int allInCount;
        private int raiseCount;

        private bool isAlwaysAllIn;
        private bool isAlwaysRaise;

        private readonly IHandEvaluator handEvaluator = new HandEvaluator();
        private CardValuationType ownCardsStrength = 0;
        private HandRankType currentHandRank;
        private ICollection<Card> hand;
        private ICollection<PlayerActionType> opponentActions = new List<PlayerActionType>();

        public override string Name { get; } = "AlwaysCallDummyPlayer_" + Guid.NewGuid();

        public override PlayerAction GetTurn(GetTurnContext context)
        {
            if (!smallBlindFlag && context.RoundType == GameRoundType.PreFlop)
            {
                smallBlindFlag = true;
                isSmallBlind = context.MyMoneyInTheRound == context.SmallBlind;
            }

            // antiCrash prefix - do not delete
            if (context.MoneyLeft <= 0)
            {
                return PlayerAction.CheckOrCall();
            }

            if (context.PreviousRoundActions.Any())
            {
                this.lastAction = context.PreviousRoundActions.Last().Action.Type;
            }

            // collecting info for opponent
            if (context.PreviousRoundActions.Any() && context.SmallBlind <= 2 && context.RoundType != GameRoundType.PreFlop)
            {
                this.opponentActions.Add(context.PreviousRoundActions.Last().Action.Type);
            }

            if (this.opponentActions.Any() && !flag && context.SmallBlind == 5)
            {
                flag = true;
                isCallingStation = this.FindCallingStation(this.opponentActions);

                isVeryAggressive = this.FindAggressiveStation(this.opponentActions);
            }

            // get current Rank
            if (context.RoundType != GameRoundType.PreFlop)
            {
                this.currentHandRank = this.GetCurrentHandRank();
            }

            // fishing prefix - nais :)
            if (!this.isAlwaysAllIn ||
                (context.MoneyToCall < MagicFishingNumber &&
                context.RoundType != GameRoundType.PreFlop))
            {
                // catching AlwaysAllIn and AlwaysRaisePlayer
                if (context.SmallBlind == 1)
                {
                    if (!this.isAlwaysAllIn && context.MoneyToCall > 990)
                    {
                        this.allInCount++;
                        if (this.allInCount >= MagicNumber + 2)
                        {
                            this.isAlwaysAllIn = true;
                        }
                        if (this.ownCardsStrength < CardValuationType.Strong)
                        {
                            return PlayerAction.Fold();
                        }
                    }
                    if (!this.isAlwaysRaise && context.MoneyToCall == context.SmallBlind)
                    {
                        this.raiseCount++;
                        if (this.raiseCount >= MagicNumber + 2)
                        {
                            this.isAlwaysRaise = true;
                        }
                    }
                }

                if (context.RoundType != GameRoundType.River)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (this.currentHandRank >= HandRankType.Straight && this.CommunityImproved())
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }

                if (this.ownCardsStrength >= CardValuationType.Strong
                    && this.DoIt(this.hand, HandRankType.Straight) < 4
                    && this.handEvaluator.GetBestHand(this.CommunityCards).RankType < HandRankType.Pair
                    && this.currentHandRank >= HandRankType.TwoPairs)
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

        private bool CommunityImproved()
        {
            var currentHand = this.handEvaluator.GetBestHand(this.hand);
            var community = this.handEvaluator.GetBestHand(this.CommunityCards);

            return currentHand.CompareTo(community) > 0;
        }

        private HandRankType GetCurrentHandRank()
        {
            this.hand = this.CommunityCards.ToList();
            this.hand.Add(this.FirstCard);
            this.hand.Add(this.SecondCard);

            return this.handEvaluator.GetBestHand(this.hand).RankType;
        }

        // renamed while explosions on testing server to aviod exploition
        private int DoIt(ICollection<Card> currentHand, HandRankType target)
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

        private PlayerAction RiverLogic(GetTurnContext context)
        {
            this.currentHandRank = this.handEvaluator.GetBestHand(this.hand).RankType;

            if (isCallingStation && this.currentHandRank > HandRankType.Pair &&
                this.lastAction < PlayerActionType.Raise)
            {
                return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
            }

            if (isVeryAggressive &&
                this.lastAction == PlayerActionType.Raise
                && context.MoneyToCall <= ((context.CurrentPot / 2) + 1))
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

            if (this.lastAction == PlayerActionType.CheckCall && !isCallingStation)
            {
                return PlayerAction.Raise((context.CurrentPot / 2) + MagicNumber);
            }

            // TODO: add handrank
            // huge miss- if we are first what? - to handle and change to this.lastAction
            if (context.PreviousRoundActions.Any()
                    && this.lastAction == PlayerActionType.Raise)
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
                if (this.lastAction < PlayerActionType.Raise)
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
                outs = this.DoIt(this.hand, HandRankType.Straight);
            }

            if (outs > 11)
            {
                if (this.lastAction == PlayerActionType.Raise)
                {
                    if (context.MoneyToCall * 100 / context.CurrentPot < outs * 2)
                    {
                        if (isVeryAggressive)
                        {
                            return PlayerAction.Raise(AllIn(context.MoneyLeft));
                        }

                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                return PlayerAction.Raise(((context.CurrentPot * 2) / 3) + MagicNumber);
            }

            if (isVeryAggressive && this.currentHandRank == HandRankType.Pair &&
                context.MoneyToCall <= context.CurrentPot * 2 / 3)
            {
                return PlayerAction.CheckOrCall();
            }

            if (outs < 8)
            {
                if (this.lastAction == PlayerActionType.Raise && context.MoneyToCall * 100 / context.CurrentPot > outs * 2)
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
                if (this.lastAction < PlayerActionType.Raise)
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
                outs = this.DoIt(this.hand, HandRankType.ThreeOfAKind);
            }
            else if (this.currentHandRank < HandRankType.Straight)
            {
                outs = this.DoIt(this.hand, HandRankType.Straight);
            }

            if (this.lastAction == PlayerActionType.Raise)
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
                if (this.lastAction < PlayerActionType.Raise &&
                    context.MoneyToCall * 100 / context.CurrentPot < outs * 5)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Raise((context.CurrentPot * 2 / 3) - MagicNumber);
            }

            if (outs < 6)
            {
                if (this.lastAction == PlayerActionType.Raise)
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
            // handling AlwaysAllIn Player
            if (this.isAlwaysAllIn && context.CurrentPot + context.MoneyLeft == 2000)
            {
                if (context.SmallBlind <= 3)
                {
                    if (this.ownCardsStrength > CardValuationType.Recommended)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                if (this.ownCardsStrength >= CardValuationType.Recommended ||
                    this.FirstCard.Type >= CardType.King ||
                    this.SecondCard.Type >= CardType.King)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Fold();
            }

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
                    if (isCallingStation)
                    {
                        return PlayerAction.Raise((context.CurrentPot * 7) - MagicNumber);
                    }

                    if (isVeryAggressive)
                    {
                        PlayerAction.Raise((context.CurrentPot * 2) + MagicNumber);
                    }

                    return PlayerAction.Raise((context.CurrentPot * 10) - MagicNumber);
                }

                if (this.ownCardsStrength >= CardValuationType.Recommended)
                {
                    if (isCallingStation)
                    {
                        return PlayerAction.Raise((10 * context.SmallBlind) - MagicNumber);
                    }

                    return PlayerAction.Raise((6 * context.SmallBlind) - MagicNumber);
                }

                if (this.ownCardsStrength == CardValuationType.Risky)
                {
                    if (isCallingStation || this.isAlwaysRaise)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Raise((5 * context.SmallBlind) - MagicNumber);
                }

                if (this.ownCardsStrength == CardValuationType.NotRecommended)
                {
                    // drops 70-80 - not good
                    //if (isAlwaysRaise)
                    //{
                    //    return PlayerAction.Fold();
                    //}

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

                if (this.ownCardsStrength == CardValuationType.Monster)
                {
                    return PlayerAction.Raise(Math.Min(((2 * context.MoneyToCall) + MagicNumber) + context.CurrentPot, AllIn(context.MoneyLeft)));
                }

                if (context.MoneyLeft <= context.SmallBlind * 20)
                {
                    if (this.FirstCard.Type == CardType.Ace || this.SecondCard.Type == CardType.Ace
                        || this.ownCardsStrength >= CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise(AllIn(context.MoneyLeft));
                    }

                    return PlayerAction.Fold();
                }

                if (this.ownCardsStrength >= CardValuationType.Recommended)
                {
                    if (context.MoneyToCall <= context.MoneyLeft / 8)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    if (this.ownCardsStrength == CardValuationType.Strong && context.MoneyToCall <= context.MoneyLeft / 5)
                    {
                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                if (isSmallBlind && context.MoneyToCall <= context.MoneyLeft * 0.2)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Fold();
            }

            // Big blind
            if (!isSmallBlind && context.PreviousRoundActions.Count >= 3)
            {
                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise)
                {
                    if (this.ownCardsStrength <= CardValuationType.NotRecommended)
                    {
                        if (this.isAlwaysRaise)
                        {
                            return PlayerAction.CheckOrCall();
                        }

                        return PlayerAction.Fold();
                    }
                }

                if (this.ownCardsStrength == CardValuationType.Risky)
                {
                    if (context.MoneyToCall > context.MoneyLeft * 0.2)
                    {
                        return PlayerAction.Fold();
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
                        return PlayerAction.Raise((2 * context.SmallBlind) + MagicNumber);
                    }

                    if (this.ownCardsStrength == CardValuationType.NotRecommended)
                    {
                        return PlayerAction.CheckOrCall();
                    }
                }

                if (context.PreviousRoundActions.Last().Action.Type == PlayerActionType.Raise
                    && this.ownCardsStrength > CardValuationType.NotRecommended)
                {
                    if (this.ownCardsStrength > CardValuationType.Recommended)
                    {
                        return PlayerAction.Raise(Math.Min(context.CurrentPot + (2 * context.MoneyToCall), context.MoneyLeft));
                    }

                    if (this.ownCardsStrength == CardValuationType.Recommended)
                    {
                        if (context.MoneyToCall < context.MoneyLeft && context.MoneyToCall < context.SmallBlind * 8)
                        {
                            return PlayerAction.CheckOrCall();
                        }

                        return PlayerAction.Fold();
                    }

                    if (this.ownCardsStrength == CardValuationType.Risky)
                    {
                        if (context.MoneyToCall > context.MoneyLeft * 0.1 && context.MoneyToCall > context.SmallBlind * 5)
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