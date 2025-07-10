# ManilaBuild
![banner](./assets/banner.png)

ManilaBuild is a powerful and flexible build automation tool designed to streamline your development workflow. Inspired by modern build systems, it uses simple JavaScript files for configuration, allowing for easy and intuitive project setup. Whether you're working on a simple C++ application or a complex multi-project solution, ManilaBuild provides the tools you need to manage dependencies, define build tasks, and automate your entire process.

## Getting Started: Your First Project

Ready to dive in? Here's how you can set up your first "Hello World" project with ManilaBuild.

### 1\. Project Structure

First, let's create a simple directory structure for our project.

```
/my-awesome-project
|--/client
|  |--/src
|     |--/main
|        |--/Client
|           |-- Main.cpp
|-- Manila.js
```

### 2\. The Source Code

Create a simple C++ source file in `src/main/Client/Main.cpp`:

```cpp
#include <iostream>

int main() {
    std::cout << "Hello World!" << std::endl;
    return 0;
}
```

### 3\. The Build Script

Now, let's create our build script. In the root of your project, create a file named `Manila.js`. This file will tell ManilaBuild how to build our project.

```javascript
const project = Manila.getProject()
const workspace = Manila.getWorkspace()
const config = Manila.getConfig()

// Apply the C++ console application plugin
Manila.apply('shiron.manila:cpp@1.0.0:console')

// Define project metadata
version('1.0.0')
description('My Awesome Project')

// Configure output directories
binDir(workspace.getPath().join('bin', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))
objDir(workspace.getPath().join('bin-int', config.getPlatform(), `${config.getConfig()}-${config.getArchitecture()}`, project.getName()))

// Define our source files
sourceSets({
	main: Manila.sourceSet(project.getPath().join('src/main')).include('**/*.cpp'),
})

// Create a task to build the project
Manila.task('build').execute(() => {
	Manila.build(workspace, project, config)
})

// Create a task to run the project
Manila.task('run')
    .after('build') // Make sure we build before running
    .execute(() => {
	    Manila.run(project)
    })
```

### 4\. The Workspace Script
The last thing you need is a global workspace script. It is used for setting build configurations that can be applied to each project.
```javascript
const workspace = Manila.getWorkspace()

Manila.onProject(['client', 'core'], p => {
    p.setToolChain(ToolChain.Clang)
})

```

### 5\. Build and Run\!

That's it\! Now you can build and run your project from the command line:

```bash
manila run :client:run
```

You should see the output: `Hello World!`

### 6\. Dive Deeper
In the meanwhile where the buildsystem is still evolvong rather quickly, you can checkout examples inside the `run/` directory.
I will also test new features inside this directory.

-----

## Key Concepts

ManilaBuild is built around a few core concepts:
  * **Workspaces and Projects**: A workspace contains one or more projects, each with its own build configuration. This allows you to manage complex, multi-project solutions with ease.
  * **Tasks**: Tasks are the fundamental building blocks of your build process. You can define tasks for compiling, running, testing, or any other action you need to perform. Tasks can also have dependencies on other tasks.
  * **Plugins**: ManilaBuild is highly extensible through plugins. The C++ plugin, for example, provides components for building static libraries and console applications. You can even create your own plugins to add new functionality.
  * **JavaScript Configuration**: Build scripts are written in JavaScript, giving you the full power and flexibility of a real programming language for your build logic.

We're excited to see what you build with Manila\!
