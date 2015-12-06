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

        private static bool isSmallBlind;
        private static bool flag;
        private static bool smallBlindFlag;
        private static bool isCallingStation;
        private static bool isVeryAggressive;



        private readonly IHandEvaluator handEvaluator = new HandEvaluator();
        private readonly ICollection<PlayerActionType> opponentActions = new List<PlayerActionType>();

        private PlayerActionType? lastAction;

        private int allInCount;
        private int raiseCount;

        private bool isAlwaysAllIn;
        private bool isAlwaysRaise;
        private CardValuationType ownCardsStrength = 0;
        private BestHand currentBestHand;
        private ICollection<Card> hand;
        private ICollection<Card> outs = new List<Card>();



        public override string Name { get; } = "AlwaysCallDummyPlayer_" + Guid.NewGuid();

        private HandRankType CurrentHandRank => this.currentBestHand.RankType;

        public override PlayerAction GetTurn(GetTurnContext context)
        {
            // antiCrash prefix - do not delete
            if (context.MoneyLeft <= 0)
            {
                return PlayerAction.CheckOrCall();
            }

            if (!smallBlindFlag && context.RoundType == GameRoundType.PreFlop)
            {
                smallBlindFlag = true;
                isSmallBlind = context.MyMoneyInTheRound == context.SmallBlind;
            }

            if (context.RoundType == GameRoundType.PreFlop)
            {
                this.outs.Clear();
            }

            this.lastAction = context.PreviousRoundActions.Any() ?
                context.PreviousRoundActions.Last().Action.Type :
                PlayerActionType.Fold;

            // collecting info for opponent
            if (context.PreviousRoundActions.Any() && context.SmallBlind <= 10 && context.RoundType != GameRoundType.PreFlop)
            {
                if (context.CurrentPot >= 10)
                {
                    this.opponentActions.Add(context.PreviousRoundActions.Last().Action.Type);
                }
            }

            if (this.opponentActions.Any() && (!flag && context.SmallBlind == 2) ||
                (flag && context.SmallBlind == 10))
            {
                flag = true;
                isCallingStation = this.FindCallingStation(this.opponentActions);

                isVeryAggressive = this.FindAggressiveStation(this.opponentActions);
            }

            // get current Rank
            if (context.RoundType != GameRoundType.PreFlop)
            {
                this.currentBestHand = this.GetCurrentBestHand();
            }

            // catching AlwaysRaisePlayer
            if (context.SmallBlind == 1 &&
                !this.isAlwaysRaise &&
                context.RoundType == GameRoundType.PreFlop &&
                (context.PreviousRoundActions.Count == 3 ||
                context.PreviousRoundActions.Count == 4))
            {
                if (!this.isAlwaysRaise && context.MoneyToCall == 1)
                {
                    this.raiseCount++;
                    if (this.raiseCount > MagicNumber)
                    {
                        this.isAlwaysRaise = true;
                    }
                }
            }

            // fishing prefix - handles fish and always raise player
            if ((this.isAlwaysRaise && !this.isAlwaysAllIn) ||
                (context.MoneyToCall < MagicFishingNumber &&
                context.RoundType != GameRoundType.PreFlop))
            {
                if (context.RoundType != GameRoundType.River)
                {
                    if (context.RoundType == GameRoundType.Turn)
                    {
                        if (this.FirstCard.Type == this.SecondCard.Type)
                        {
                            this.DoIt(this.hand, HandRankType.ThreeOfAKind);
                        }
                        else if (this.CurrentHandRank < HandRankType.Straight &&
                            this.FirstCard.Type != this.SecondCard.Type)
                        {
                            this.DoIt(this.hand, HandRankType.Straight);
                        }
                    }

                    return PlayerAction.CheckOrCall();
                }

                if (context.RoundType == GameRoundType.River && this.CurrentHandRank >= HandRankType.Straight && this.CommunityImproved())
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }

                if (this.ownCardsStrength >= CardValuationType.Strong
                    && this.outs.Count < 5
                    && this.handEvaluator.GetBestHand(this.CommunityCards).RankType < HandRankType.Pair
                    && this.CurrentHandRank >= HandRankType.TwoPairs)
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
            var community = this.handEvaluator.GetBestHand(this.CommunityCards);

            return this.currentBestHand.CompareTo(community) > 0;
        }

        private BestHand GetCurrentBestHand()
        {
            this.hand = this.CommunityCards.ToList();
            this.hand.Add(this.FirstCard);
            this.hand.Add(this.SecondCard);

            return this.handEvaluator.GetBestHand(this.hand);
        }

        // renamed while explosions on testing server to aviod exploition
        private void DoIt(ICollection<Card> currentHand, HandRankType target)
        {
            foreach (var card in Deck.AllCards)
            {
                if (currentHand.Contains(card) || this.outs.Contains(card))
                {
                    continue;
                }

                currentHand.Add(card);

                if (this.handEvaluator.GetBestHand(currentHand).RankType >= target)
                {
                    this.outs.Add(card);
                }

                currentHand.Remove(card);
            }
        }

        private PlayerAction RiverLogic(GetTurnContext context)
        {
            if (isCallingStation && this.CurrentHandRank > HandRankType.Pair &&
                this.lastAction < PlayerActionType.Raise)
            {
                return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
            }

            if (isVeryAggressive &&
                this.lastAction == PlayerActionType.Raise
                && context.MoneyToCall <= ((context.CurrentPot / 2) + 1))
            {
                if (this.FirstCard.Type >= CardType.King || this.SecondCard.Type >= CardType.King)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (this.CurrentHandRank >= HandRankType.Straight && this.CommunityImproved())
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }
            }

            if (this.lastAction < PlayerActionType.Raise && !isCallingStation && !this.isAlwaysRaise)
            {
                return PlayerAction.Raise((context.CurrentPot / 3) + MagicNumber);
            }

            // TODO: add handrank
            if (this.lastAction == PlayerActionType.Raise)
            {
                if (this.CurrentHandRank >= HandRankType.Straight && this.CommunityImproved())
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.CurrentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.CheckOrCall();
                }

                if (!isCallingStation && context.MoneyToCall <= context.CurrentPot / 2 && this.CurrentHandRank >= HandRankType.Pair)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Fold();
            }

            if (this.CurrentHandRank >= HandRankType.TwoPairs && this.CommunityImproved())
            {
                return PlayerAction.Raise(context.CurrentPot + MagicNumber);
            }

            return PlayerAction.CheckOrCall();
        }

        private bool FindCallingStation(ICollection<PlayerActionType> actions)
        {
            int calls = actions.Count(x => x == PlayerActionType.CheckCall);
            if (actions.Any() && (calls * 100 / actions.Count) > 73)
            {
                return true;
            }

            return false;
        }

        private bool FindAggressiveStation(ICollection<PlayerActionType> actions)
        {
            int raises = actions.Count(x => x == PlayerActionType.Raise);
            if (actions.Any() && raises * 100 / actions.Count > 61)
            {
                return true;
            }

            return false;
        }

        private PlayerAction TurnLogic(GetTurnContext context)
        {
            if (isCallingStation && this.CurrentHandRank > HandRankType.Pair)
            {
                return PlayerAction.Raise((context.CurrentPot * 2) + MagicNumber);
            }

            if (this.CurrentHandRank >= HandRankType.TwoPairs)
            {
                if (this.lastAction < PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.CurrentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.lastAction < PlayerActionType.Raise && !isCallingStation && !this.isAlwaysRaise)
            {
                return PlayerAction.Raise((context.CurrentPot / 3) + MagicNumber);
            }

            if (this.CurrentHandRank < HandRankType.Straight)
            {
                this.DoIt(this.hand, HandRankType.Straight);
            }

            if (this.outs.Count > 10)
            {
                if (this.lastAction == PlayerActionType.Raise)
                {
                    if (context.MoneyToCall * 100 / context.CurrentPot < (this.outs.Count * 2) + 5)
                    {
                        if (isVeryAggressive && !this.isAlwaysRaise)
                        {
                            return PlayerAction.Raise(AllIn(context.MoneyLeft));
                        }

                        return PlayerAction.CheckOrCall();
                    }

                    return PlayerAction.Fold();
                }

                return PlayerAction.Raise(((context.CurrentPot * 2) / 3) + MagicNumber);
            }

            if (isVeryAggressive && this.CurrentHandRank >= HandRankType.Pair &&
                context.MoneyToCall <= context.CurrentPot * 2 / 3)
            {
                return PlayerAction.CheckOrCall();
            }

            if (this.outs.Count <= 10)
            {
                if (this.lastAction == PlayerActionType.Raise &&
                    context.MoneyToCall * 100 / context.CurrentPot > this.outs.Count * 2)
                {
                    return PlayerAction.Fold();
                }
            }

            return PlayerAction.CheckOrCall();
        }

        private PlayerAction FlopLogic(GetTurnContext context)
        {
            if (isCallingStation && this.CurrentHandRank > HandRankType.Pair)
            {
                return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
            }

            if (isVeryAggressive)
            {
                if (this.CurrentHandRank >= HandRankType.TwoPairs)
                {
                    return PlayerAction.Raise((context.MoneyToCall * 2) + MagicNumber);
                }

                if (context.MoneyToCall <= context.CurrentPot && this.CurrentHandRank >= HandRankType.Pair)
                {
                    return PlayerAction.CheckOrCall();
                }
            }

            if (this.lastAction < PlayerActionType.Raise && !isCallingStation && !this.isAlwaysRaise)
            {
                return PlayerAction.Raise((context.CurrentPot / 3) + MagicNumber);
            }

            if (this.CurrentHandRank > HandRankType.TwoPairs)
            {
                if (this.lastAction < PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot + MagicNumber);
                }

                if (this.CurrentHandRank >= HandRankType.Straight)
                {
                    return PlayerAction.Raise((context.CurrentPot * 2) - MagicNumber);
                }

                return PlayerAction.CheckOrCall();
            }

            if (this.FirstCard.Type == this.SecondCard.Type)
            {
                this.DoIt(this.hand, HandRankType.ThreeOfAKind);
            }
            else if (this.CurrentHandRank < HandRankType.Straight &&
                this.FirstCard.Type != this.SecondCard.Type)
            {
                this.DoIt(this.hand, HandRankType.Straight);
            }

            if (this.lastAction == PlayerActionType.Raise && isVeryAggressive && !this.isAlwaysRaise)
            {
                if (this.outs.Count >= 9)
                {
                    return PlayerAction.Raise(AllIn(context.MoneyLeft));
                }
            }

            if (this.lastAction < PlayerActionType.Raise && !isCallingStation && !this.isAlwaysRaise)
            {
                return PlayerAction.Raise((context.CurrentPot / 3) + MagicNumber);
            }

            if (this.outs.Count > 11)
            {
                if (this.lastAction < PlayerActionType.Raise)
                {
                    return PlayerAction.Raise(context.CurrentPot * 2 / 3 + MagicNumber);
                }
                else
                {
                    return PlayerAction.CheckOrCall();
                }
            }

            if (this.outs.Count > 8)
            {
                if (this.lastAction < PlayerActionType.Raise &&
                    context.MoneyToCall * 100 / context.CurrentPot < this.outs.Count * 5)
                {
                    return PlayerAction.CheckOrCall();
                }

                return PlayerAction.Raise((context.CurrentPot * 2 / 3) - MagicNumber);
            }

            if (this.outs.Count < 6)
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
            this.ownCardsStrength = PreflopHandStrengthValuation.GetRecommendation(this.FirstCard, this.SecondCard);

            if (context.SmallBlind == 1)
            {
                if (!this.isAlwaysAllIn && context.CurrentPot + context.MoneyLeft == 2000)
                {
                    this.allInCount++;
                    if (this.allInCount > MagicNumber)
                    {
                        this.isAlwaysAllIn = true;
                    }

                    if (this.ownCardsStrength < CardValuationType.Strong)
                    {
                        return PlayerAction.Fold();
                    }
                }
            }

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
                        return PlayerAction.Raise((context.CurrentPot * 2) + MagicNumber);
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
                if (this.lastAction == PlayerActionType.Raise)
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

                if (this.lastAction == PlayerActionType.CheckCall)
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

                if (this.lastAction == PlayerActionType.Raise
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