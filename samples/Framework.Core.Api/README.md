# Framework.Core.Api

This is a standalone external-consumer sample for the Aspros Basic Framework .NET 10 package line.

The API consumes all five Framework NuGet packages through `PackageReference`: Abstractions, Domain, Infrastructure, Algorithms and ClickHouse. It does not use `ProjectReference` to the Framework source tree.

Run the packaged-consumer release check from the repository root:

```bash
bash scripts/verify-packaged-consumer.sh
```

The active Docker API workflow uses `Framework.Core.Provider` and `Framework.Core.Consumer`; see `dev-environment/README.md`.
