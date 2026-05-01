namespace Game.Domain;

public enum CombatWinner
{
    Attacker,
    Defender
}

public sealed record CombatInput(
    int AttackerStrength,
    int DefenderStrength,
    int TerritoryDefense,
    int AttackPosition,
    int DefensePosition);

public sealed record CombatResult(
    int EffectiveAttackerStrength,
    int EffectiveDefenderStrength,
    CombatWinner Winner,
    int SurvivingStrength);

public static class CombatCalculator
{
    public static CombatResult Resolve(CombatInput input)
    {
        var attacker = Round(input.AttackerStrength * PositionModifier(input.AttackPosition));
        var defender = Round(
            input.DefenderStrength *
            (1 + input.TerritoryDefense / 200m) *
            PositionModifier(input.DefensePosition));

        var winner = attacker >= defender
            ? CombatWinner.Attacker
            : CombatWinner.Defender;

        var winningEffectiveStrength = Math.Max(attacker, defender);
        var losingEffectiveStrength = Math.Min(attacker, defender);
        var winningOriginalStrength = winner == CombatWinner.Attacker
            ? input.AttackerStrength
            : input.DefenderStrength;
        var marginRatio = winningEffectiveStrength == 0
            ? 0
            : (winningEffectiveStrength - losingEffectiveStrength) / (decimal)winningEffectiveStrength;
        var survivingStrength = Round(winningOriginalStrength * marginRatio);

        return new CombatResult(attacker, defender, winner, survivingStrength);
    }

    private static decimal PositionModifier(int positionScore) =>
        1 + positionScore / 400m;

    private static int Round(decimal value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
