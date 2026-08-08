using System.Collections.Generic;
using Vintagestory.API.Common;
using VintageStory.API.Server;

namespace LongLife;

public class LongLifeMod : ModSystem
{
    private ICoreServerAPI? serverApi;

    private readonly Dictionary<string, SurvivalPlayerData> playerData = new();
    private readonly Dictionary<string, double> lastWorldTimes = new();

    private void OnPlayerJoin(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        playerData.TryAdd(uid, new SurvivalPlayerData());

        lastWorldTimes[uid] = serverApi!.World.Calendar.TotalDays;

        Mod.Logger.Notification(
            $"{player.PlayerName} joined at day {lastWorldTimes[uid]:F2}"
        );
    }

    private void OnPlayerDeath(IServerPlayer player)
    {
        if (!playerData.TryGetValue(player.PlayerUID, out SurvivalPlayerData? data))
            return;

        data.SurvivalDays = 0;

        lastWorldTimes[player.PlayerUID] =
            serverApi!.World.Calendar.TotalDays;
    }

    private void UpdatePlayers(float dt)
    {
        if (serverApi == null)
            return;

        double currentWorldTime = serverApi.World.Calendar.TotalDays;

        foreach (IServerPlayer player in serverApi.World.AllOnlinePlayers)
        {
            string uid = player.PlayerUID;

            if (!playerData.TryGetValue(uid, out SurvivalPlayerData? data))
                continue;

            if (!lastWorldTimes.TryGetValue(uid, out double lastTime))
            {
                lastWorldTimes[uid] = currentWorldTime;
                continue;
            }

            double elapsed = currentWorldTime - lastTime;

            if (elapsed > 0)
            {
                data.SurvivalDays = Math.Min(
                    data.SurvivalDays + elapsed,
                    60.0
                );
            }

            lastWorldTimes[uid] = currentWorldTime;
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverApi = api;

        api.Event.PlayerJoin += OnPlayerJoin;
        api.Event.RegisterGameTickListener(UpdatePlayers, 1000);

        Mod.Logger.Notification("Survival Progression initialized!");
    }
}