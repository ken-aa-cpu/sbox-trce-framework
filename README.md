# TRCE Framework

**A Spigot-inspired plugin framework for s&box.**  
Build modular, maintainable multiplayer games — the way Minecraft server developers do it.

---

## What is TRCE?

TRCE is a server-side plugin framework built on top of s&box. It gives developers a structured, decoupled way to build game features as independent plugins that can coexist, communicate, and be maintained without breaking each other.

If you've ever written a Spigot plugin for Minecraft, TRCE will feel familiar.

| Spigot | TRCE |
|--------|------|
| `JavaPlugin` | `TrcePlugin` |
| `EventHandler` | `GlobalEventBus` |
| `ServicesManager` | `TrceServiceManager` |
| `PlaceholderAPI` | `IPlaceholderService` (PAPI) |
| Vault Economy | `IEconomyService` |
| Vault Permission | `IPermissionService` |

---

## Why TRCE?

s&box is a powerful engine, but it doesn't come with a plugin ecosystem. TRCE solves three problems that every multiplayer game developer eventually runs into:

**API changes break everything.**  
s&box updates frequently. TRCE wraps all engine calls inside a single `SandboxBridge` layer — when the API changes, you fix one file, not fifty.

**Game features become tangled.**  
Without structure, your combat system ends up depending on your economy system, which depends on your UI, which depends on everything else. TRCE enforces a clean plugin architecture where features communicate through interfaces, not direct references.

**Memory leaks are silent killers.**  
Forgetting to unsubscribe from events is one of the most common bugs in s&box. TRCE's `RegisterEvent` system automatically unsubscribes every handler when a plugin is disabled — you can't forget, even if you try.

---

## Architecture Overview

```
┌─────────────────────────────┐
│         Games Layer          │  Your game modes live here
├─────────────────────────────┤
│        Plugins Layer         │  Feature plugins (Combat, Economy, Social...)
├─────────────────────────────┤
│         Kernel Layer         │  Framework core — service contracts & infrastructure
├─────────────────────────────┤
│       SandboxBridge          │  The only place that touches s&box APIs directly
└─────────────────────────────┘
```

The Kernel defines **what** services exist (interfaces). Plugins define **how** they work (implementations). Your game just uses them.

---

## What You Can Build

**Game mode plugins** — Round management, phase systems, win conditions.  
**Economy plugins** — Currency, shops, battle passes, transactions.  
**Combat plugins** — Weapons, hit validation, health systems, kill feeds.  
**Social plugins** — Chat formatting, teams, roles, confrontation systems.  
**Inventory plugins** — Item stacks, containers, persistent storage.  
**Permission plugins** — Groups, nodes, wildcard permissions, timed grants.  
**Placeholder plugins** — Dynamic text like `%economy_balance%` in any UI string.

Everything built as a plugin, everything independently maintainable.

---

## Quick Start

### 1. Create a plugin

```csharp
using Trce.Kernel.Plugin;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin.Services;
using System.Threading.Tasks;

[TrcePlugin(Id = "mygame.rewards", Name = "Reward Plugin", Version = "1.0.0", Author = "YourName")]
public class RewardPlugin : TrcePlugin
{
    private IEconomyService _economy;

    protected override async Task OnPluginEnabled()
    {
        // Get services from other plugins
        _economy = GetService<IEconomyService>();

        // Subscribe to events — auto-unsubscribed when plugin is disabled
        RegisterEvent<CoreEvents.PlayerKilledEvent>(OnPlayerKilled);

        await Task.CompletedTask;
    }

    private void OnPlayerKilled(CoreEvents.PlayerKilledEvent e)
    {
        _economy?.Add(e.AttackerSteamId, 100f, "Kill reward");
    }

    protected override void OnPluginDisabled()
    {
        _economy = null;
    }
}
```

### 2. Provide a service

```csharp
[TrcePlugin(Id = "mygame.economy", Name = "Economy Plugin", Version = "1.0.0")]
public class EconomyPlugin : TrcePlugin, IEconomyService
{
    protected override async Task OnPluginEnabled()
    {
        // Register yourself so other plugins can find you
        TrceServiceManager.Instance?.RegisterService<IEconomyService>(this);
        await Task.CompletedTask;
    }

    protected override void OnPluginDisabled()
    {
        TrceServiceManager.Instance?.UnregisterService<IEconomyService>();
    }

    // Implement IEconomyService...
    public float GetBalance(ulong steamId) { ... }
    public float Add(ulong steamId, float amount, string reason = "") { ... }
}
```

### 3. Use placeholders in UI

```csharp
var papi = GetService<IPlaceholderService>();
string text = papi.Parse("Balance: %economy_balance% coins", playerObject);
// → "Balance: 500 coins"
```

---

## Core Systems

### Event Bus
Zero-allocation, thread-safe global event system. Events are `readonly struct` — no heap allocation on publish.

```csharp
// Publish
GlobalEventBus.Publish(new CoreEvents.PlayerKilledEvent(victimId, attackerId, hitPos));

// Subscribe (inside a plugin — auto-unsubscribed on disable)
RegisterEvent<CoreEvents.PlayerKilledEvent>(OnPlayerKilled);
```

### Service Locator
O(1) dictionary lookup. No singletons, no scene searches, no coupling.

```csharp
var health = GetService<IHealthService>();
var perms  = GetService<IPermissionService>();
```

### Attribute System
Stackable modifiers with dirty-flag caching. Formula: `(base + flat modifiers) × percent modifiers`.

```csharp
stats.SetBaseValue(steamId, "speed", 100f);
stats.AddModifier(steamId, "speed", AttributeModifier.Flat(50f));     // +50
stats.AddModifier(steamId, "speed", AttributeModifier.Percent(2.0f)); // ×2
float speed = stats.GetTotalValue(steamId, "speed"); // 300
```

### State Tags
Time-limited status effects backed by s&box's native tag system.

```csharp
tags.AddTag(player, "stunned", durationSeconds: 3f); // auto-removes after 3s
bool isStunned = tags.HasTag(player, "stunned");
```

### Permission System
Wildcard nodes, group inheritance, timed grants.

```csharp
perm.GrantPermission(steamId, "admin.kick", TimeSpan.FromDays(7));
bool canKick = perm.HasPermission(steamId, "admin.kick");
// "admin.*" grants access to all admin.* nodes
```

---

## Built-in Services

| Interface | Description |
|-----------|-------------|
| `IEconomyService` | Currency — balance, add, deduct, transfer |
| `IHealthService` | Health — damage, heal, death check |
| `IInventoryService` | Inventory — async item management |
| `IPawnService` | Pawn spawning and management |
| `IWeaponService` | Weapon give/take/check |
| `IPlayerService` | Player info — name, team, role |
| `IGameModeService` | Game mode state — phase, round active |
| `IPermissionService` | Permissions — nodes, groups, wildcards |
| `IPlaceholderService` | PAPI — dynamic text parsing |
| `IAttributeService` | Numeric attributes with modifiers |
| `IStateTagService` | Timed status tags |
| `IContainerService` | Item container management |
| `IModelService` | Model and animation control |

---

## Running Tests

TRCE includes in-game unit tests. Run them from the s&box console:

```
trce_test_stat        # TrceStatPlugin — 10 tests
trce_test_eventbus    # EntityEventBus — 12 tests
```

---

## Rules for Plugin Developers

```
✅ Communicate between plugins through interfaces only
✅ Use RegisterEvent — never GlobalEventBus.Subscribe directly
✅ Unregister your services in OnPluginDisabled
✅ Use SandboxBridge for all engine API calls
✅ Name your plugin classes with the Trce prefix
✅ Define a TrcePluginAttribute with a unique Id

❌ Don't import another plugin's class directly
❌ Don't put game logic in the Kernel layer
❌ Don't use s&box whitelist-blocked APIs (ReaderWriterLockSlim, etc.)
```

---

## Firebase Setup

TRCE supports Firebase Realtime Database for persistent storage. To configure:

1. Create a `firebase_config.json` file in your s&box data directory:

```json
{
  "databaseUrl": "https://your-project.firebaseio.com",
  "authSecret": "your-secret-here"
}
```

2. Add `firebase_config.json` to your `.gitignore` — **never commit this file**.

The config is loaded server-side only. Clients never see it.

---

## License

TRCE Framework is open source under the following terms:

- **Free to use** — personal and commercial projects welcome
- **Free to modify** — extend it, fork it, build on top of it
- **Attribution required** — keep the original copyright notice
- **No reselling the framework** — you may not sell TRCE Framework itself (modified or not) as a standalone product

Plugins and games built on TRCE are entirely yours. No restrictions on what you build with it.

---

## Credits

Built by the TRCE Team.  
Inspired by the Spigot plugin ecosystem.  
Running on [s&box](https://sbox.game) by Facepunch Studios.  
Current limitations: Architecture refining, Security hardening. Feel free to contribute.
