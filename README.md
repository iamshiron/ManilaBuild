# ManilaBuild
![banner](./assets/banner.png)
[![CI](https://github.com/iamshiron/ManilaBuild/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/iamshiron/ManilaBuild/actions/workflows/ci.yml)
[![Code Quality](https://github.com/iamshiron/ManilaBuild/actions/workflows/code-quality.yml/badge.svg?branch=main)](https://github.com/iamshiron/ManilaBuild/actions/workflows/code-quality.yml)

ManilaBuild is a powerful and flexible build automation tool designed to streamline your development workflow. Inspired by modern build systems, it uses simple JavaScript files for configuration, making project setup easy and intuitive. Whether you're working on a simple C++ application or a complex multi-project solution, ManilaBuild provides the tools you need to manage dependencies, define build jobs, and automate your entire process.

-----

## ❗ Heavy Development ❗

**Please be aware that ManilaBuild is currently in heavy development and is not yet feature-complete.** The API is subject to change, and you may encounter bugs. We appreciate your patience and welcome any feedback.

**The examples provided are mock-ups to demonstrate how the API is intended to work.**

If you want to see a real-world example, look at the `Zip` project under the `/run` directory, which creates a zip archive from a source set.

-----

## Getting Started: Your First Project

Ready to dive in? Here’s how to set up your first project with ManilaBuild. This example will create a simple zip archive.

### 1. Project Structure

First, create the following directory structure for your project.

```
/my-awesome-workspace
|-- /zip
|   |-- Manila.js       # Project-level configuration
|   `-- /src
|       `-- /main       # Your source set
|           |-- somefile.txt
|           `-- another.txt
`-- Manila.js           # Workspace-level configuration
```

### 2. The Source Code

Go ahead and create some files and sub-folders inside `src/main`. Their contents will be included in the final zip archive.

### 3. The Build Script

Now, let's create the project-level build script. In the root of your `zip` project (`my-awesome-workspace/zip/`), create a file named `Manila.js`. This file tells ManilaBuild how to build your project.

```javascript
const project = Manila.getProject();
const workspace = Manila.getWorkspace();

// Apply the zip plugin
Manila.apply('shiron.manila:zip/zip');
const config = Manila.getConfig();

project.version('1.0.0');
project.description('Demo Project Core');

project.sourceSets({
    main: Manila.sourceSet(project.GetPath().join('src/main')).include('**/*.*')
});

// Define the project's artifacts
project.Artifacts({
    // Define the 'main' artifact
    main: Manila.artifact((artifact) => { // The 'artifact' object is passed to the callback
        Manila.job('build').execute(async () => {
            // This is where the project is built
            await Manila.build(workspace, project, config, artifact);
            print('Zip file created!');
        });
    })
    .from('shiron.manila:zip/zip')
    .description('Awesome Zip Artifact')
});
```

### 4. The Workspace Script

Finally, add a workspace-level script to the root of your workspace (`my-awesome-workspace/Manila.js`). This file is used for global configurations that apply to all projects. For now, it can be minimal.

**Keep in mind:** This file also acts as a marker that identifies the directory as a ManilaBuild workspace. You must create this file, even if it's empty.

```javascript
const workspace = Manila.getWorkspace();
```

### 5. Build and Run!

That's it! Now you can build your project from the command line:

```bash
manila run zip/main:build
```

This command follows the format `manila run <project-name>/<artifact-name>:<job-name>`. In our case, we're executing the `build` job defined in the `main` artifact of the `zip` project.

You should see output as the zip file is built, followed by your `Zip file created!` message and a final confirmation that the build was successful.

-----

## Key Concepts

ManilaBuild is built around a few core concepts:

  * **Workspaces and Projects**: A workspace contains one or more projects, each with its own build configuration. This allows you to easily manage complex, multi-project solutions.
  * **Jobs**: Jobs are the fundamental building blocks of your build process. You can define jobs for compiling, testing, or any other action. Jobs can also have dependencies on other jobs.
  * **Plugins**: ManilaBuild is highly extensible through plugins. The C++ plugin, for example, provides components for building static libraries and console applications. You can also create your own plugins to add new functionality.
  * **JavaScript Configuration**: Build scripts are written in JavaScript, giving you the full power and flexibility of a real programming language for your build logic.

-----

## Dive Deeper

While the build system is still rapidly evolving, you can check out more examples inside the `run/` directory to see how new features are tested and implemented.
