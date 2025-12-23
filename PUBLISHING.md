# Publishing Selena

This guide explains how to publish Selena to GitHub and NuGet.

## Prerequisites

- [ ] .NET SDK 8.0 or later
- [ ] Git installed and configured
- [ ] GitHub account
- [ ] NuGet account (for NuGet.org publishing)
- [ ] Visual Studio 2022 or VS Code (optional)

## Step 1: Prepare the Repository

1. **Update Version Number**
   - Edit `src/Selena/Selena.csproj` and update the `<Version>` tag
   - Update `CHANGELOG.md` with release notes

2. **Replace Placeholders**
   - Replace `yourusername` with your GitHub username in all files
   - Add actual PNG icon file to replace `icon.png` placeholder
   - Update email addresses in documentation

3. **Run Tests**
   ```powershell
   .\build.ps1 -Test
   ```
   Or on Unix:
   ```bash
   ./build.sh --test
   ```

4. **Build Package Locally**
   ```powershell
   .\build.ps1 -Pack -Version 1.0.0
   ```

## Step 2: Create GitHub Repository

1. Go to https://github.com/new
2. Repository name: `Selena`
3. Description: "High-Performance Inter-Process Communication for .NET"
4. Make it public
5. Don't initialize with README (we already have one)

## Step 3: Push to GitHub

```bash
git init
git add .
git commit -m "Initial commit: Selena Library v1.0.0"
git branch -M develop
git remote add origin https://github.com/Taiizor/Selena.git
git push -u origin develop
```

## Step 4: Setup GitHub Actions Secrets

1. Go to repository Settings → Secrets and variables → Actions
2. Add the following secrets:
   - `NUGET_API_KEY`: Your NuGet.org API key

## Step 5: Create Release

1. Go to Releases → Create a new release
2. Tag: `v1.0.0`
3. Title: "Selena v1.0.0 - Initial Release"
4. Description: Copy from CHANGELOG.md
5. Publish release

This will trigger the CI/CD pipeline to:
- Build on all platforms
- Run tests
- Publish to NuGet.org
- Create GitHub packages

## Step 6: Verify Publication

### On NuGet.org
1. Go to https://www.nuget.org/packages/Selena
2. Verify package is listed
3. Check package details

### Test Installation
```bash
dotnet new console -n TestSelena
cd TestSelena
dotnet add package Selena
```

## Step 7: Post-Publication

1. **Update Documentation**
   - Add shields.io badges with actual links
   - Update installation instructions
   - Add link to NuGet package

2. **Announce Release**
   - Twitter/X
   - Reddit (r/dotnet, r/csharp)
   - Dev.to article
   - LinkedIn

3. **Monitor Issues**
   - Watch for bug reports
   - Respond to questions
   - Plan next release

## Maintenance Checklist

### For Each Release
- [ ] Update version numbers
- [ ] Update CHANGELOG.md
- [ ] Run all tests
- [ ] Update documentation
- [ ] Create GitHub release
- [ ] Verify NuGet package
- [ ] Announce release

### Regular Tasks
- [ ] Review and merge PRs
- [ ] Respond to issues
- [ ] Update dependencies
- [ ] Run security audits
- [ ] Update benchmarks

## Troubleshooting

### Build Fails on CI
- Check GitHub Actions logs
- Verify all target frameworks are available
- Check for missing dependencies

### NuGet Push Fails
- Verify API key is correct
- Check package ID is not taken
- Ensure version is higher than existing

### Tests Fail on CI
- Check for platform-specific issues
- Verify file paths are cross-platform
- Check for timing issues in tests

## Support

For help with publishing:
- GitHub Actions: https://docs.github.com/actions
- NuGet: https://docs.microsoft.com/nuget
- .NET: https://docs.microsoft.com/dotnet
