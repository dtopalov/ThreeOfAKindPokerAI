namespace TexasHoldem.AI.SmartPlayer
{
    using System;

    using Logic;
    using Logic.Extensions;
    using Logic.Players;

    using Stages;

    // TODO: HQC
    public class ThreeOfAKind : BasePlayer
    {
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
            throw new NotImplementedException();
        }

        private PlayerAction PreflopLogic(GetTurnContext context)
        {
            // to rework
            var playHand = PreflopHandStrengthValuation.GetDesicion(this.FirstCard, this.SecondCard);
            if (playHand == CardValuationType.Unplayable)
            {
                if (context.CanCheck)
                {
                    return PlayerAction.CheckOrCall();
                }
                return PlayerAction.Fold();
            }

            if (playHand == CardValuationType.Risky)
            {
                var smallBlindsTimes = RandomProvider.Next(1, 8);
                return PlayerAction.Raise(context.SmallBlind * smallBlindsTimes);
            }

            if (playHand == CardValuationType.Recommended)
            {
                var smallBlindsTimes = RandomProvider.Next(6, 14);
                return PlayerAction.Raise(context.SmallBlind * smallBlindsTimes);
            }

            return PlayerAction.CheckOrCall();
        }
    }
}
