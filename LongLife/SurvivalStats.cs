namespace LongLife;

public static class SurvivalStats
{
    public static double GetBonus(SurvivalPlayerData data)
    {
        return data.SurvivalDays * 0.01;
    }
}