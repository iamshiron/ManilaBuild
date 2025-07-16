# GitHub Actions Workflows

Optimized CI/CD workflows for the Manila project with caching for faster builds and free tier efficiency.

## Workflows

### 🧪 CI (`ci.yml`)
Full build and test pipeline for main development branches. Includes cross-platform testing on Windows and macOS for main branch pushes.

### ⚡ Fast PR Check (`pr-check.yml`)
Quick validation for pull requests with smart change detection. Skips unnecessary builds for documentation-only changes.

### 🔍 Code Quality (`code-quality.yml`)
Build validation with warnings treated as errors to maintain code quality standards.

### 🛡️ Security Scan (`security.yml`)
Weekly vulnerability scanning for NuGet packages with automated reporting.

## Performance Features

- **NuGet & Build Caching**: Dramatically faster subsequent runs
- **Smart Triggers**: Cross-platform testing only when needed
- **Minimal Overhead**: Optimized for GitHub's free tier limits
- **Change Detection**: Skip builds for non-code changes

## Setup

Workflows are ready to use immediately - no additional configuration required.
