# Testing

Automated tests live in `Journeys.Tests`. Follow existing folders (RulesEngine, Workflow).

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj
```

`agent-verify.ps1` runs tests when a changed file is under `Journeys.Tests\` or `-RunTests` is set.

Wrapper persist tests in `Utility/WrappedEventPayloadPersistTests.cs` assert canonical lowercase JSON symbols, nested engine-state names (`nodememberships`), and that empty list properties are omitted.
