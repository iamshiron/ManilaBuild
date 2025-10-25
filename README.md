# ManilaBuild
![banner](./assets/banner.png)
[![CI](https://github.com/iamshiron/ManilaBuild/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/iamshiron/ManilaBuild/actions/workflows/ci.yml)
[![Code Quality](https://github.com/iamshiron/ManilaBuild/actions/workflows/code-quality.yml/badge.svg?branch=main)](https://github.com/iamshiron/ManilaBuild/actions/workflows/code-quality.yml)

ManilaBuild is a powerful and flexible build automation tool designed to streamline your development workflow. Inspired by modern build systems, it uses simple JS files for configuration, making project setup easy and intuitive. Whether you're working on a simple C++ application or a complex multi-project solution, ManilaBuild provides the tools you need to manage dependencies, define build jobs, and automate your entire process.

-----

## ❗ Heavy Development ❗

**Please be aware that ManilaBuild is currently in heavy development and is not yet feature-complete.** The API is subject to change, and you may encounter bugs. We appreciate your patience and welcome any feedback.

**The examples provided are mock-ups to demonstrate how the API is intended to work.**

If you want to see a real-world example, look at the `Zip` project under the `/run` directory, which creates a zip archive from a source set.

-----

## Getting Started: Your First Project

Ready to dive in? Here’s how to set up your first project with ManilaBuild. This example will create a simple zip archive.

### 1. Project Setup

First, create a new empty folder that will contain your workspace.
Inside this folder, run `manila init` to initialize a new empty workspace.

### Environment variables

- MANILA_CACHE_HOST: The cache server base URL including port (for example: http://localhost:3000). When set, artifact caches are pushed to this server after successful builds.
- MANILA_CACHE_KEY: Optional bearer token used for authenticating with the cache server when authentication is enabled. If omitted and the server enforces auth, uploads will be skipped with a warning.

### 2. Project Creation

To create your first project, we will use the *Zip project template* as an example.
To create this project, just type `manila new your_project zip:default`.
You will now see a new directory with the name of your project, containing a build script to creat the zip file and a test file.

### 3. Build!

That's it! Now you can build your project from the command line:

```bash
manila run your_project/main:build
```

This command follows the format `manila run <project-name>/<artifact-name>:<job-name>`. In our case, we're executing the `build` job defined in the `main` artifact of the `your_project` project.

You should see output as the zip file is built, followed by your `Zip file created!` message and a final confirmation that the build was successful.

-----

## Key Concepts

ManilaBuild is built around a few core concepts:

  * **Workspaces and Projects**: A workspace contains one or more projects, each with its own build configuration. This allows you to easily manage complex, multi-project solutions.
  * **Jobs**: Jobs are the fundamental building blocks of your build process. You can define jobs for compiling, testing, or any other action. Jobs can also have dependencies on other jobs.
  * **Plugins**: ManilaBuild is highly extensible through plugins. To test the plugin system, the only available plugin for now is a Zip plugin. This is just for testing and won't end up in the final version.
  * **JavaScript Configuration**: Build scripts are written in JavaScript, giving you the full power and flexibility of a real programming language for your build logic.

-----

## Dive Deeper

While the build system is still rapidly evolving, you can check out more examples inside the `run/` directory to see how new features are tested and implemented.
