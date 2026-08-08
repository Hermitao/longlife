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

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverApi = api;

        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.PlayerLeave += OnPlayerLeave;
        api.Event.PlayerDeath += OnPlayerDeath;

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

        lastWorldTimes[uid] =
            serverApi!.World.Calendar.TotalDays;

        lastLoggedDays[uid] =
            (int)Math.Floor(survivalDays[uid]);

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

    private void OnPlayerDeath(
        IServerPlayer player,
        DamageSource damageSource
    )
    {
        string uid = player.PlayerUID;

        // Reset LongLife survival time.
        survivalDays[uid] = 0.0;

        // Prevent time between death and respawn from being counted.
        if (serverApi != null)
        {
            lastWorldTimes[uid] =
                serverApi.World.Calendar.TotalDays;
        }

        // Reset all LongLife modifiers.
        UpdateLongLifeStats(player);

        Mod.Logger.Notification(
            $"LongLife: {player.PlayerName} died. " +
            $"Survival time reset to 0 days."
        );
    }

    private void UpdateSurvivalTime(float dt)
    {
        if (serverApi == null)
            return;

        double currentWorldTime =
            serverApi.World.Calendar.TotalDays;

        foreach (IServerPlayer player in
                 serverApi.World.AllOnlinePlayers)
        {
            string uid = player.PlayerUID;

            if (!survivalDays.ContainsKey(uid))
            {
                survivalDays[uid] = 0.0;
            }

            if (!lastWorldTimes.TryGetValue(
                    uid,
                    out double lastWorldTime))
            {
                lastWorldTimes[uid] = currentWorldTime;
                continue;
            }

            double elapsed =
                currentWorldTime - lastWorldTime;

            // Only count forward movement of the game clock.
            if (elapsed > 0)
            {
                survivalDays[uid] = Math.Min(
                    survivalDays[uid] + elapsed,
                    60.0
                );

                UpdateLongLifeStats(player);

                // Temporary diagnostic.
                int currentDay =
                    (int)Math.Floor(survivalDays[uid]);

                if (
                    currentDay > 0 &&
                    currentDay !=
                    lastLoggedDays.GetValueOrDefault(uid)
                )
                {
                    lastLoggedDays[uid] = currentDay;

                    EntityFloatStats? healthStat =
                        GetStat(
                            player,
                            "maxhealthExtraPoints"
                        );

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
                            "LongLife: Could not find " +
                            "maxhealthExtraPoints."
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

        // 1 point of bonus per in-game day, capped at 60.
        //
        // 0 days  -> 0
        // 1 day   -> 1
        // 30 days -> 30
        // 60 days -> 60
        float bonus =
            (float)Math.Min(days, 60.0);

        // Health should gain at most 9 extra HP.
        //
        // 0 days  -> 0 HP
        // 30 days -> 4.5 HP
        // 60 days -> 9 HP
        float healthBonus =
            bonus * (9f / 60f);

        EntityFloatStats? healthStat =
            GetStat(
                player,
                "maxhealthExtraPoints"
            );

        if (healthStat != null)
        {
            healthStat.Set(
                LongLifeModifier,
                healthBonus
            );
        }

        // Other stats.
        //
        // These are NOT percentages.
        // Adjust these values according to how much you want
        // each stat to increase per survival day.
        string[] multiplierStats =
        {
            "rangedWeaponsDamage",
            "meleeWeaponsDamage",
            "animalLootDropRate",
            "forageDropRate",
            "wildCropDropRate",
            "oreDropRate",
            "miningSpeedMul"
        };

        foreach (string statName in multiplierStats)
        {
            EntityFloatStats? stat =
                GetStat(player, statName);

            if (stat == null)
            {
                Mod.Logger.Warning(
                    $"LongLife: Could not find stat " +
                    $"'{statName}' for player " +
                    $"{player.PlayerName}."
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