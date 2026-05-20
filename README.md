<div align="center">

# CoreECS

**State-first Entity–Component–System toolkit for C# games**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%20netstandard2.1-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PR Build and Test](https://github.com/Cardidi/CoreECS/actions/workflows/pr-build-test.yml/badge.svg)](https://github.com/Cardidi/CoreECS/actions/workflows/pr-build-test.yml)

Lightweight ECS you can embed beside Unity ECS or other stacks — built around **ComponentStore**, **EntityGraph**, and **structural-change collectors**.

[Quick Start Guide](docs/QUICK_START.md) · [License](LICENSE) · [NuGet](https://www.nuget.org/packages/CoreECS)

</div>

---

## Features

| Area | Highlights |
|------|------------|
| **Architecture** | `ComponentStore` + `EntityGraph` for flexible, compact component storage |
| **State-first** | `EntityCollector` with `Flush()`, `Matching` / `Clashing` / `Changed` buffers |
| **Queries** | Fluent `EntityMatcher` (`OfAll`, `OfAny`, `OfNone`, mask filtering) |
| **Components** | `RO` / `RW` refs, optional `OnCreate` / `OnDestroy`, extension helpers |
| **Systems** | Ordered execution, `TickGroup` masks, constructor DI via `IInjectionProxy` |
| **Targets** | `net8.0` and `netstandard2.1` |

---

## Installation

```bash
dotnet add package CoreECS
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) (see `global.json` for SDK pinning).

```bash
git clone https://github.com/Cardidi/CoreECS.git
cd CoreECS
dotnet build
dotnet test
```

**New to the API?** Follow the [**Quick Start Guide**](docs/QUICK_START.md) — world lifecycle, components, systems, matchers, collectors, and a full sample.

---

## At a Glance

```csharp
using CoreECS;

var world = new World();
world.Startup();

world.RegisterSystem<MovementSystem>();

var entity = world.CreateEntity();
entity.CreateComponent(new PositionComponent { X = 0, Y = 0 });
entity.CreateComponent(new VelocityComponent { X = 10, Y = 5 });

world.BeginTick();
world.Tick();
world.EndTick();

world.Shutdown();
```

See [docs/QUICK_START.md](docs/QUICK_START.md) for collectors, flags, matchers, and DI setup.

---

## Why a Toolkit, Not a Framework?

Game logic often repeats across genres and budgets — card games, RPGs, multiplayer, indie prototypes. A monolithic framework can steer design toward “does the framework support X?” instead of “what does the game need?”

CoreECS grew from a turn-based card project that needed **predictable state** and **change tracking**, while Unity ECS handled high-throughput simulation. Unity ECS is strong on performance but rigid for state-driven workflows; CoreECS fills that gap as a **loosely coupled toolkit** you compose where it fits — simulation helper, state guardian, or standalone ECS loop.

---

## Key Concepts

| Concept | Role |
|---------|------|
| **Entity** | Stable id grouping components (`Entity` struct over `ulong`) |
| **Component** | Data struct (`IComponent<T>`), logic lives in systems |
| **System** | `ISystem` — `OnCreate` / `OnTick` / `OnDestroy` |
| **World** | Lifecycle, entities, components, systems, collectors |
| **Matcher** | `EntityMatcher` filters by components and entity mask |
| **Collector** | Tracks matcher matches; defers buffers until `Flush()` |
| **InjectionProxy** | DI for system constructors (`RegisterServices`) |
| **Tick** | `BeginTick` → `Tick(mask)` → `EndTick` |
| **Mask** | Bit flags on entities/systems for filtered ticks and queries |

---

## Documentation

| Document | Description |
|----------|-------------|
| [**Quick Start Guide**](docs/QUICK_START.md) | Full tutorial: 11 sections from world setup to complete example |
| [**AGENTS.md**](AGENTS.md) | Build commands and contributor notes for agents/CI |

---

## Project Layout

```
CoreECS/
├── ECS/          # CoreECS library (net8.0 + netstandard2.1)
├── Test/         # NUnit tests
├── docs/         # Guides (Quick Start, …)
└── README.md     # This file
```

---

## License

MIT — see [LICENSE](LICENSE).
