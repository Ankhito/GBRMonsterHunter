using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using GBRMonsterHunter.IPC;

namespace GBRMonsterHunter.Planning;

internal enum MonsterNavigationState
{
    Idle,
    Teleporting,
    WaitingForZoneLoad,
    Navigating,
    ArrivedSearching,
    Arrived,
    Failed,
}

internal sealed class MonsterNavigator(
    PluginServices services,
    Configuration config,
    LifestreamIpc lifestream,
    VnavmeshIpc vnavmesh,
    RotationDriverService rotationDriver,
    CommandBridge commands,
    MonsterRoutePlanner planner)
{
    private const double TeleportCooldownSeconds = 3.0;
    private const double ZoneLoadWaitSeconds = 1.5;
    private const double TargetSearchRetrySeconds = 2.0;

    private MonsterLocation? target;
    private AetheryteRoute? route;
    private DateTime stateStartedAt = DateTime.MinValue;
    private DateTime nextTargetSearchAt = DateTime.MinValue;
    private bool teleportAttempted;
    private int targetSearchAttempts;

    public MonsterNavigationState State { get; private set; }
    public string StatusText { get; private set; } = "Idle";

    private float ArrivalDistance => Math.Clamp(config.ArrivalDistance, 2f, 50f);
    private float TargetSearchRadius => Math.Clamp(config.TargetSearchRadius, 5f, 100f);
    private double NavigationTimeoutSeconds => Math.Clamp(config.NavigationTimeoutSeconds, 30.0, 900.0);
    private double TargetSearchTimeoutSeconds => Math.Clamp(config.TargetSearchTimeoutSeconds, 5.0, 120.0);

    public bool Start(MonsterLocation location)
    {
        route = planner.ResolveRoute(location);
        if (route == null)
        {
            State = MonsterNavigationState.Failed;
            StatusText = "No usable route/aetheryte found for monster location.";
            return false;
        }

        target = location;
        teleportAttempted = false;
        targetSearchAttempts = 0;
        nextTargetSearchAt = DateTime.MinValue;
        State = services.ClientState.TerritoryType == route.TerritoryTypeId
            ? MonsterNavigationState.WaitingForZoneLoad
            : MonsterNavigationState.Teleporting;
        stateStartedAt = DateTime.UtcNow;
        StatusText = State == MonsterNavigationState.Teleporting
            ? $"Teleporting to {route.AetheryteName}"
            : "Preparing local navigation";
        return true;
    }

    public void Stop()
    {
        lifestream.Abort();
        vnavmesh.Stop();
        rotationDriver.ResumeCombat();
        target = null;
        route = null;
        teleportAttempted = false;
        targetSearchAttempts = 0;
        nextTargetSearchAt = DateTime.MinValue;
        State = MonsterNavigationState.Idle;
        StatusText = "Idle";
    }

    public void Update()
    {
        if (target == null || route == null || State is MonsterNavigationState.Idle or MonsterNavigationState.Arrived or MonsterNavigationState.Failed)
            return;

        switch (State)
        {
            case MonsterNavigationState.Teleporting:
                UpdateTeleporting();
                break;
            case MonsterNavigationState.WaitingForZoneLoad:
                UpdateWaitingForZoneLoad();
                break;
            case MonsterNavigationState.Navigating:
                UpdateNavigating();
                break;
            case MonsterNavigationState.ArrivedSearching:
                UpdateArrivedSearching();
                break;
        }
    }

    private void UpdateTeleporting()
    {
        if (IsBetweenAreas() || lifestream.IsBusy())
            return;

        if (!teleportAttempted)
        {
            teleportAttempted = true;
            stateStartedAt = DateTime.UtcNow;
            StatusText = $"Teleporting to {route!.AetheryteName}";

            lifestream.RefreshAvailability();
            if (lifestream.Available)
                lifestream.ExecuteCommand(route.AetheryteName);
            else
                commands.TeleporterTeleport(route.AetheryteName, config.TeleporterCommandTemplate);
            return;
        }

        if ((DateTime.UtcNow - stateStartedAt).TotalSeconds < TeleportCooldownSeconds)
            return;

        if (services.ClientState.TerritoryType == route!.TerritoryTypeId)
        {
            State = MonsterNavigationState.WaitingForZoneLoad;
            stateStartedAt = DateTime.UtcNow;
            StatusText = "Waiting for zone load";
        }
        else if ((DateTime.UtcNow - stateStartedAt).TotalSeconds > 30)
        {
            State = MonsterNavigationState.Failed;
            StatusText = $"Teleport timeout; still in territory {services.ClientState.TerritoryType}, expected {route.TerritoryTypeId}.";
        }
    }

    private void UpdateWaitingForZoneLoad()
    {
        if (IsBetweenAreas() || lifestream.IsBusy())
            return;

        if ((DateTime.UtcNow - stateStartedAt).TotalSeconds < ZoneLoadWaitSeconds)
            return;

        StartVnavmeshNavigation();
    }

    private void StartVnavmeshNavigation()
    {
        vnavmesh.RefreshAvailability();
        if (!vnavmesh.Available || !vnavmesh.IsReady())
        {
            State = MonsterNavigationState.Failed;
            StatusText = $"vnavmesh unavailable: {vnavmesh.LastError ?? "navmesh not ready"}";
            return;
        }

        var destination = vnavmesh.NearestPoint(route!.Destination) ?? route.Destination;
        if (!vnavmesh.PathfindAndMoveCloseTo(destination, ArrivalDistance))
        {
            State = MonsterNavigationState.Failed;
            StatusText = $"Failed to start vnavmesh movement: {vnavmesh.LastError ?? "unknown error"}";
            return;
        }

        State = MonsterNavigationState.Navigating;
        stateStartedAt = DateTime.UtcNow;
        StatusText = $"Moving to {target!.MobName}";
    }

    private void UpdateNavigating()
    {
        if (services.ClientState.TerritoryType != route!.TerritoryTypeId)
        {
            State = MonsterNavigationState.Teleporting;
            teleportAttempted = false;
            stateStartedAt = DateTime.UtcNow;
            StatusText = "Territory changed; restarting route";
            return;
        }

        var player = services.Objects.LocalPlayer;
        if (player == null)
            return;

        var distance = System.Numerics.Vector3.Distance(player.Position, route.Destination);
        if (distance <= ArrivalDistance)
        {
            HandleArrival();
            return;
        }

        if ((DateTime.UtcNow - stateStartedAt).TotalSeconds > NavigationTimeoutSeconds)
        {
            vnavmesh.Stop();
            State = MonsterNavigationState.Failed;
            StatusText = $"Navigation timeout; still {distance:F1} yalms away.";
            return;
        }

        if (!vnavmesh.IsNavigating())
        {
            StatusText = $"Movement stopped; restarting ({distance:F1} yalms remaining)";
            StartVnavmeshNavigation();
        }
    }

    private void HandleArrival()
    {
        vnavmesh.Stop();
        if (TrySelectHuntedTarget())
        {
            CompleteArrivalWithTarget();
            return;
        }

        State = MonsterNavigationState.ArrivedSearching;
        stateStartedAt = DateTime.UtcNow;
        nextTargetSearchAt = DateTime.UtcNow;
        targetSearchAttempts = 0;
        StatusText = $"Arrived, searching for {target!.MobName}.";
    }

    private void UpdateArrivedSearching()
    {
        if (services.ClientState.TerritoryType != route!.TerritoryTypeId)
        {
            State = MonsterNavigationState.Teleporting;
            teleportAttempted = false;
            stateStartedAt = DateTime.UtcNow;
            StatusText = "Territory changed; restarting route";
            return;
        }

        if (IsBetweenAreas() || lifestream.IsBusy())
            return;

        if (DateTime.UtcNow < nextTargetSearchAt)
            return;

        targetSearchAttempts++;
        if (TrySelectHuntedTarget())
        {
            CompleteArrivalWithTarget();
            return;
        }

        var elapsed = (DateTime.UtcNow - stateStartedAt).TotalSeconds;
        if (elapsed > TargetSearchTimeoutSeconds)
        {
            StatusText = $"Could not find {target!.MobName} after {elapsed:F0}s; retrying route.";
            StartVnavmeshNavigation();
            return;
        }

        nextTargetSearchAt = DateTime.UtcNow.AddSeconds(TargetSearchRetrySeconds);
        StatusText = $"Arrived, searching for {target!.MobName} ({targetSearchAttempts} attempt(s)).";
    }

    private void CompleteArrivalWithTarget()
    {
        var driverReady = rotationDriver.PrepareForCombat();
        State = MonsterNavigationState.Arrived;
        StatusText = driverReady
            ? $"Targeted {target!.MobName}; {rotationDriver.DriverName} combat driver ready."
            : $"Targeted {target!.MobName}; combat driver unavailable ({rotationDriver.LastError ?? rotationDriver.StatusDetail}).";
    }

    private bool TrySelectHuntedTarget()
    {
        if (target == null || route == null)
            return false;

        var searchRadius = TargetSearchRadius;
        var match = services.Objects
            .Where(obj => obj.ObjectKind == ObjectKind.BattleNpc && obj.IsTargetable)
            .Select(obj => new
            {
                Object = obj,
                IdMatches = target.BNpcNameId != null && obj.BaseId == target.BNpcNameId.Value,
                NameMatches = string.Equals(obj.Name.ToString(), target.MobName, StringComparison.OrdinalIgnoreCase),
                Distance = System.Numerics.Vector3.Distance(obj.Position, route.Destination),
                DistanceSquared = System.Numerics.Vector3.DistanceSquared(obj.Position, route.Destination),
            })
            .Where(candidate => candidate.Distance <= searchRadius)
            .Where(candidate => candidate.IdMatches || candidate.NameMatches)
            .OrderByDescending(candidate => candidate.IdMatches)
            .ThenByDescending(candidate => candidate.NameMatches)
            .ThenBy(candidate => candidate.DistanceSquared)
            .Select(candidate => candidate.Object)
            .FirstOrDefault();

        if (match == null)
            return false;

        services.Targets.Target = match;
        return true;
    }

    private bool IsBetweenAreas() =>
        services.Condition[ConditionFlag.BetweenAreas] || services.Condition[ConditionFlag.BetweenAreas51];
}
