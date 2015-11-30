namespace TexasHoldem.AI.ThreeOfAKind
{
    public enum CardValuationType
    {
        Unplayable = 0,
        NotRecommended = 1000,
        Risky = 2000,
        Recommended = 3000,
        Strong = 4000,
        Monster = 5000
    }
}
