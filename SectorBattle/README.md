# Sector Battle Prototype

This is a deterministic campaign battle backend that exists alongside the current tile battle system.
It does not replace or modify tile-battle resolution.

## Selecting the backend

Set `DeterministicBattleManager.BattleSystemMode` to `Sector` in the campaign scene. `TileBased`
remains the default, so existing scenes and serialized data keep their current behaviour.

## Battlefield model

The battlefield contains twenty-five coarse sectors: five tactical lanes (`TopFlank`, `UpperWing`,
`Centre`, `LowerWing`, `BottomFlank`) by five horizontal depths (`SideAReserve`, `SideALine`,
`CentralGround`, `SideBLine`, `SideBReserve`). Formations have a sector but no precise position
inside it. Side A advances left-to-right and Side B right-to-left. Normal deployment distributes
formations across Upper Wing, Centre, and Lower Wing in their own reserve; the outer flanks begin
empty as manoeuvre space. Sector control is derived from active occupants:
empty, side A, side B, or contested.

Combat in a contested sector uses the sector's base frontage. Excess formations remain reserves
and automatically enter active frontage when a participating formation is destroyed or routes.
Friendly formations in an uncontested adjacent lane add separate flank frontage while
remaining in their own sector. Uncontested ranged formations can support contested sectors within
the configured sector range.

All simulation state uses integer ticks and deterministic ordering. Projectile visuals, animations,
and other presentation are intentionally non-authoritative.

## Configuration and extension points

- `Resources/SectorBattleRules.asset` contains tick rate, frontage, terrain, exhaustion, morale,
  ranged support, flanking, phalanx, Warchief, Warcry, Disciplined, and opening-volley values.
- `ISectorCombatAbility` allows additional data-driven combat and exhaustion modifiers without
  adding more special cases to the resolver.
- Unit combat definitions are produced by the existing `TileBattleCampaignAdapter`, so weapons,
  armour, shields, tags, formation types, ammunition, and base unit statistics remain shared.
- `IBattleSimulation` is the common backend contract. `TileBattleSimulationAdapter` exposes the
  existing tile simulation through it; `SectorBattleSimulation` implements it directly.

## Testing and inspection

Open **Project X > Sector Battle Debugger**. The deterministic 10-vs-10 example deploys both forces
across their respective reserve wings and centre and exposes their automatically generated command groups. The window can step one tick, run ten ticks, or resolve the
battle, and lists control, frontage, active formations, reserves, flank contributors, ranged
supporters, and the combat event log.

The EditMode test fixture is `SectorBattleTests`. It verifies repeatable hashes, reserve deployment,
command capacity, membership locking, and synchronized group movement.

## Play-mode battlefield viewer

`SectorBattlePresentation` is attached automatically by `SectorBattleCampaignManager`. While at
least one sector battle is active, the existing scene-authored **Tile Battles** button becomes a
backend-neutral **BATTLES** button and includes sector battles in its count. It opens the relevant
tile or sector viewer depending on which backend owns the active encounter.
Opening and closing it does not tick, pause, reset, or otherwise mutate the simulation.

Campaign sector encounters also create the same small world-map battle marker used by the older
battle presentation. Clicking that marker first opens an **ARMIES ENGAGED** popup listing every
participating army or garrison and its current formation composition. **View Battlefield** then
opens the sector viewer. Reinforcement armies appear in the popup as soon as they join.

Development builds and the Editor also show **CUSTOM SECTOR BATTLE** (or press `F8`). The harness
can prepare five Rome-versus-Carthage presets, set a deterministic seed, choose Player/AI control
per side, and set each general's command capacity from four to eight groups. Before starting, units
may be split, moved between compatible groups, or merged. Restarting preserves the chosen seed.

During battle, click a player-controlled formation to select its entire command group. Valid
adjacent sectors are highlighted; click one to move the group at its slowest member's pace. Hold,
Advance, and Withdraw are also available in the group panel. AI sides use the same group-order API
and receive no formation-level movement privileges.

The viewer provides:

- a spatial five-lane by five-depth battlefield with terrain-coloured sector areas;
- sector borders, control shading, frontage, active/reserve counts, ranged supporters, and
  side-specific flank frontage/source labels;
- one existing layered and animated unit representative per formation, with faction recolouring;
- separate contact, support, ranged-support, idle, and movement positions inside sectors;
- interpolated movement, purpose-based facing, attack animation triggers, and flank arrows;
- hover or click inspection for strength, morale, exhaustion, sector, and current role;
- mouse-wheel zoom and middle-mouse panning; and
- a toggle for sector debug labels and flank indicators.

The presentation reads simulation snapshots and structured presentation events. It never selects
combatants, calculates frontage, assigns roles, or resolves attacks itself.

## Prototype limits

The campaign host resolves battles in the background and applies formation survivors, levy losses,
garrison conquest, engagement cooldowns, and winner movement delay. Active sector battles do not
yet have a replay/history timeline, save payload, or multiplayer replication. The current
battlefield is a strategy-style overlay rather than a separate world-space
scene/camera; this keeps the campaign running underneath it and avoids existing scene-switch/pause
coupling. Future features should consume the interface/event output rather than becoming part of
authoritative combat.

Prototype pacing is deliberately slow for inspection: the default rules run at two simulation ticks
per campaign second and resolve combat once every three ticks. Both values are editable in
`Resources/SectorBattleRules.asset`.
