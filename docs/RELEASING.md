# Releasing

This fork packages `AgentUsageWidget.exe` in
`artifacts/release/agent-usage-widget-win-x64.zip`. The assembly name changed;
the project path and version source remain `src/CodexUsageWidget/CodexUsageWidget.csproj`.
Local packaging does not publish anything. Do not reuse upstream tags for a modified
fork release; choose a new version and tag when a release is explicitly requested.

GitHub Releases are created automatically from version tags. The project version
in `src/CodexUsageWidget/CodexUsageWidget.csproj` is the release version source of
truth.

## Publish a version

1. Update `<Version>` in `CodexUsageWidget.csproj`.
2. Commit and push the change to `master`.
3. Wait for the CI workflow to pass.
4. Create and push an annotated tag matching the project version:

   ```powershell
   git tag -a v1.1.0 -m "Release v1.1.0"
   git push origin v1.1.0
   ```

The release workflow validates that the tag and project version match, runs the
test suite, creates the self-contained Windows x64 package, generates its SHA-256
checksum, and publishes both files on GitHub Releases.

Release tags identify immutable published versions and must not be moved or
reused. If a release needs a correction, publish a new patch version instead.

If pushing a tag does not start the Release workflow, run the same workflow
manually against `master`, supplying the existing tag:

```powershell
gh workflow run release.yml --ref master -f tag=v1.7.2
```

The manual run checks out that tag and performs the same version validation,
tests, packaging, and publishing steps. Do not run it for an already published tag.
