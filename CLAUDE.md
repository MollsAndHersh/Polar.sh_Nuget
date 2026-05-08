# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

This repository will contain a .NET NuGet package integrating with [Polar.sh](https://polar.sh) — an open source monetization platform. The package is expected to target .NET and follow standard NuGet packaging conventions.

## Required Reading Order

Before doing any work, read these files (from the home directory `/Users/mollsandhersh/`):

1. `AGENTS.md` — workflow rules, agentic-master policy, RAG policy
2. `PLAN.md` — active technical plan
3. `TASKS.md` — current task list
4. `PROGRESS.md` — completed work log
5. `DECISIONS.md` — locked architecture decisions
6. `ZoranHorvat.md` — required for all .NET/C#/NuGet work in this repo

## Build and Test

> Update this section once the project structure is established.

Expected commands once `.csproj` / `.sln` files exist:

```sh
dotnet build
dotnet test
dotnet pack           # produces the NuGet .nupkg
dotnet nuget push     # publish to NuGet feed
```

## Git and GitHub Policy

Do **not** run raw mutating Git/GitHub commands. Use `agentic-master` wrappers:

```sh
agentic-master new-task <TASK-ID>
agentic-master commit --ai
agentic-master push
agentic-master finish-task
agentic-master verify
```

See `~/AGENTS.md` for the full command reference.

## Architecture Notes

> Populate this section once source files exist.

- This is a .NET library project — apply ZoranHorvat.md coding standards to all `.cs` and `.csproj` files.
- NuGet package metadata (authors, description, version, license) belongs in the `.csproj` file via `<PackageId>`, `<Version>`, `<Description>`, etc., not in a separate `nuspec`.
- Public API surface should be minimal and intentional — treat every public type as a committed contract.
