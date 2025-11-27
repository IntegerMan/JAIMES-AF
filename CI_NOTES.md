# CI Notes

- `dotnet test` runs with Coverlet and writes OpenCover data to `TestResults/coverage/coverage.opencover.xml`.
- SonarCloud scans occur in the same workflow job so coverage is uploaded automatically via `dotnet-sonarscanner`.
