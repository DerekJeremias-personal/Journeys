# Testing

Automated tests live in `Journeys.Tests`. Follow existing folders (RulesEngine, Workflow).

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj
```

`agent-verify.ps1` runs tests when a changed file is under `Journeys.Tests\` or `-RunTests` is set.

`Configuration/ConfigureInfraTests.cs` asserts the API still registers Data Lake and blob adapters when `DataLake:ConnectionString` is missing, so `builder.Build()` does not fail.

Wrapper persist tests in `Utility/WrappedEventPayloadPersistTests.cs` assert canonical lowercase JSON symbols, nested engine-state names (`nodememberships`), and that empty list properties are omitted.
