# Contributing

1. Open an issue before large changes so the design can be discussed.
2. Fork, branch from `main` (`feat/...`, `fix/...`).
3. `dotnet build` must pass with zero warnings (they are errors), `dotnet test` must be green.
4. Add or update tests for every behavior change. New public types need XML docs.
5. Never log or include secret values in findings or test fixtures. Use obviously fake placeholders.
6. Use conventional commit messages (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).
7. Open a pull request against `main`; CI must pass before review.

By contributing you agree that your contributions are licensed under the MIT license.
