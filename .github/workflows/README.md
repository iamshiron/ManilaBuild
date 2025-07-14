# GitHub Actions Workflows

This directory contains optimized GitHub Actions workflows for the Manila project, designed to minimize execution time and costs for the free tier.

## Workflows

### CI (`ci.yml`) - Optimized for Free Tier
**Triggers**: Push and PR to `main` and `develop` branches
- **Primary Ubuntu job**: Fast execution on every trigger
- **Cross-platform testing**: Only runs on main branch pushes (saves ~70% of minutes)
- **Features**:
  - .NET package caching for faster restores
  - Minimal verbosity to reduce log overhead
  - Optimized environment variables to skip telemetry

### Fast PR Check (`pr-check.yml`) - Ultra-Fast PR Validation
**Triggers**: Pull requests only
- **Smart change detection**: Skips build/test for docs-only changes
- **Minimal checkout**: Only fetches specific commit (fetch-depth: 1)
- **Lightning fast**: Completes in ~1-2 minutes for code changes

### Code Quality (`code-quality.yml`) - Essential Quality Gates
**Triggers**: Push and PR to `main` and `develop` branches
- **Format checking**: Ensures consistent code style
- **Build validation**: Treats warnings as errors
- **Optimized**: Runs on Ubuntu only with minimal verbosity

### Security Scan (`security.yml`) - Weekly Security Check
**Triggers**: Main branch only + weekly schedule
- **Reduced frequency**: Weekly instead of daily (saves ~85% of scheduled runs)
- **Targeted scanning**: Only on main branch to save minutes
- **Short artifact retention**: 7 days instead of default 90

## 🚀 Optimization Features

### Execution Time Optimizations
- **Caching**: .NET packages cached across runs
- **Minimal verbosity**: Reduced log output
- **Single platform primary**: Ubuntu for main testing (3x faster than Windows/macOS)
- **Smart triggers**: Cross-platform only on important branches

### Cost Optimizations (Free Tier Friendly)
- **Reduced matrix builds**: Only when necessary
- **Conditional execution**: Skip unnecessary jobs
- **Optimized scheduling**: Weekly instead of daily scans
- **Efficient checkout**: Shallow clones where possible

### Estimated Monthly Usage (Free Tier: 2000 minutes)
- **Daily development** (5 PRs/week): ~150 minutes
- **Weekly releases** (main pushes): ~50 minutes
- **Security scans**: ~20 minutes
- **Total estimated**: ~220 minutes/month (11% of free tier)

## Setup Requirements

No additional setup required! The workflows are ready to use out of the box.

### Secrets Configuration
Currently no secrets are required for the basic workflows to function.

## Branch Protection
Recommended branch protection rules for `main`:
- Require status checks: `test-ubuntu`, `code-quality`, `quick-check`
- Require branches to be up to date before merging
- Include administrators in restrictions

## Performance Tips
- Enable .NET caching in setup-dotnet action
- Set environment variables to skip telemetry and first-time experiences
- Use minimal verbosity to reduce log processing time
