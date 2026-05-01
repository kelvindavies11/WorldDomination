using Game.Domain;

namespace Game.Tests.Domain;

public sealed class CombatCalculatorTests
{
    [Fact]
    public void AppliesTerritoryDefenseAndPositionModifiersToBattlePower()
    {
        var battle = new CombatInput(
            AttackerStrength: 120,
            DefenderStrength: 100,
            TerritoryDefense: 50,
            AttackPosition: 80,
            DefensePosition: 40);

        var result = CombatCalculator.Resolve(battle);

        Assert.Equal(144, result.EffectiveAttackerStrength);
        Assert.Equal(138, result.EffectiveDefenderStrength);
        Assert.Equal(CombatWinner.Attacker, result.Winner);
        Assert.Equal(5, result.SurvivingStrength);
    }

    [Fact]
    public void DefenderWinsWhenTerrainAndPositionOvercomeAttacker()
    {
        var battle = new CombatInput(
            AttackerStrength: 100,
            DefenderStrength: 80,
            TerritoryDefense: 100,
            AttackPosition: 0,
            DefensePosition: 100);

        var result = CombatCalculator.Resolve(battle);

        Assert.Equal(100, result.EffectiveAttackerStrength);
        Assert.Equal(150, result.EffectiveDefenderStrength);
        Assert.Equal(CombatWinner.Defender, result.Winner);
        Assert.Equal(27, result.SurvivingStrength);
    }
}
