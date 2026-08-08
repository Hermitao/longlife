using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LongLife;

public class LongLifeMod : ModSystem
{
    private ICoreServerAPI? serverApi;

    // Survival time accumulated for each player.
    // The value is measured in in-game days.
    private readonly Dictionary<string, double> survivalDays = new();

    // The last world-time value we observed for each online player.
    private readonly Dictionary<string, double> lastWorldTimes = new();

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

        Mod.Logger.Notification(
            $"LongLife: {player.PlayerName} joined. " +
            $"Survival time: {survivalDays[uid]:F3} days."
        );
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        lastWorldTimes.Remove(uid);

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
            }

            lastWorldTimes[uid] = currentWorldTime;
        }
    }
}