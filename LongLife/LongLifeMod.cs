using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LongLife;

public class LongLifeMod : ModSystem
{
    private const string LongLifeModifier = "longlife";

    private ICoreServerAPI? serverApi;

    // Survival time accumulated for each player.
    // Measured in in-game days.
    private readonly Dictionary<string, double> survivalDays = new();

    // Last world-time value observed for each online player.
    private readonly Dictionary<string, double> lastWorldTimes = new();

    // Last whole survival day for which we printed diagnostics.
    private readonly Dictionary<string, int> lastLoggedDays = new();

    // Stats affected by LongLife.
    private static readonly string[] LongLifeStats =
    {
        "maxhealthExtraPoints",
        "rangedWeaponsDamage",
        "meleeWeaponsDamage",
        "animalLootDropRate",
        "forageDropRate",
        "wildCropDropRate",
        "oreDropRate",
        "miningSpeedMul"
    };

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverApi = api;

        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerLeave += OnPlayerLeave;

        // Update approximately once per second.
        api.Event.RegisterGameTickListener(UpdateSurvivalTime, 1000);

        Mod.Logger.Notification("LongLife initialized!");
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        if (!survivalDays.ContainsKey(uid))
        {
            survivalDays[uid] = 0.0;
        }

        lastWorldTimes[uid] = serverApi!.World.Calendar.TotalDays;

        lastLoggedDays[uid] = (int)Math.Floor(survivalDays[uid]);

        Mod.Logger.Notification(
            $"LongLife: {player.PlayerName} joined. " +
            $"Survival time: {survivalDays[uid]:F3} days."
        );

        UpdateLongLifeStats(player);
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        lastWorldTimes.Remove(uid);
        lastLoggedDays.Remove(uid);

        Mod.Logger.Notification(
            $"LongLife: {player.PlayerName} left. " +
            $"Survival time: {survivalDays.GetValueOrDefault(uid):F3} days."
        );
    }

    private void UpdateSurvivalTime(float dt)
    {
        if (serverApi == null)
            return;

        double currentWorldTime = serverApi.World.Calendar.TotalDays;

        foreach (IServerPlayer player in serverApi.World.AllOnlinePlayers)
        {
            string uid = player.PlayerUID;

            if (!survivalDays.ContainsKey(uid))
            {
                survivalDays[uid] = 0.0;
            }

            if (!lastWorldTimes.TryGetValue(uid, out double lastWorldTime))
            {
                lastWorldTimes[uid] = currentWorldTime;
                continue;
            }

            double elapsed = currentWorldTime - lastWorldTime;

            // Only count forward movement of the game clock.
            if (elapsed > 0)
            {
                survivalDays[uid] = Math.Min(
                    survivalDays[uid] + elapsed,
                    60.0
                );

                UpdateLongLifeStats(player);

                // Temporary diagnostic.
                int currentDay = (int)Math.Floor(survivalDays[uid]);

                if (
                    currentDay > 0 &&
                    currentDay != lastLoggedDays.GetValueOrDefault(uid)
                )
                {
                    lastLoggedDays[uid] = currentDay;

                    EntityFloatStats? healthStat =
                        GetStat(player, "maxhealthExtraPoints");

                    if (healthStat != null)
                    {
                        Mod.Logger.Notification(
                            $"LongLife: {player.PlayerName} reached " +
                            $"{survivalDays[uid]:F3} survival days. " +
                            $"HealthStat: {healthStat.GetBlended():F4}"
                        );
                    }
                    else
                    {
                        Mod.Logger.Warning(
                            "LongLife: Could not find maxhealthExtraPoints."
                        );
                    }
                }
            }

            lastWorldTimes[uid] = currentWorldTime;
        }
    }

    private void UpdateLongLifeStats(IServerPlayer player)
    {
        if (!survivalDays.TryGetValue(
                player.PlayerUID,
                out double days))
        {
            return;
        }

        // 1% per in-game day, capped at 60%.
        //
        // 0 days  -> 0.00
        // 1 day   -> 0.01
        // 30 days -> 0.30
        // 60 days -> 0.60
        float bonus = (float)(Math.Min(days, 60.0) / 100.0);

        foreach (string statName in LongLifeStats)
        {
            EntityFloatStats? stat = GetStat(player, statName);

            if (stat == null)
            {
                Mod.Logger.Warning(
                    $"LongLife: Could not find stat '{statName}' " +
                    $"for player {player.PlayerName}."
                );

                continue;
            }

            stat.Set(
                LongLifeModifier,
                bonus
            );
        }
    }

    private EntityFloatStats? GetStat(
        IServerPlayer player,
        string statName
    )
    {
        foreach (var stat in player.Entity.Stats)
        {
            if (stat.Key == statName)
            {
                return stat.Value;
            }
        }

        return null;
    }
}