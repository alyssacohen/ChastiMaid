# Smart project scanning suitable for a loop

Every 10 minutes, scan all directories in the ChastiMaid project.
Analyze C# files for:

1. Naming convention violations (PascalCase for classes, camelCase for variables)
2. Missing XML documentation comments (///)
3. StyleCop violations
4. Unused imports or variables

After each scan, suggest ONE improvement that follows Microsoft C# coding guidelines and Avalonia best practices.
Run 'dotnet build' before each analysis.
Print suggestions in a concise format.

Don't do more than 3 iterations.
