# ManilaBuild
![banner](./assets/banner.png)
ManilaBuild is a powerful and flexible build automation tool designed to streamline your development workflow. Inspired by modern build systems, it uses simple JavaScript files for configuration, allowing for an easy and intuitive project setup. Whether you're working on a simple C++ application or a complex multi-project solution, ManilaBuild provides the tools you need to manage dependencies, define build jobs, and automate your entire process.

-----

## ❗ Heavy Development ❗

**Please be aware that ManilaBuild is currently in heavy development and is nowhere near completion.** Features may change, and there may be bugs. We appreciate your patience and welcome any feedback you may have.

**The examples provided are mock-ups to demonstrate how the API is intended to work.**

If you wanna see a real example, look at the `Zip` project under the `/run` directory. It creates a zip file out of your source sets.

-----

## Getting Started: Your First Project

Ready to dive in? Here's how you can set up your first "Hello World" project with ManilaBuild.

### 1\. Project Structure

First, let's create a simple directory structure for our project.

```
/my-awesome-project
|--/client
|  |--Manila.js           # Project Level Configuration
|  |--/src
|     |--/main            # Your source set
|        |--/Client
|           |-- Main.cpp  # Your source file
|-- Manila.js             # Workspace Level Configuration
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

Now, let's create our project-level build script inside the `client` directory. In the root of your project, create a file named `Manila.js`. This file will tell ManilaBuild how to build our project.

```javascript
const project = Manila.getProject()
const workspace = Manila.getWorkspace()

Manila.apply('shiron.manila:cpp@1.0.0:console')
const config = Manila.getConfig()

project.Version('1.0.0')
project.Description('Demo Project Core')

project.SourceSets({
	main: Manila.sourceSet(project.GetPath().join('src/main')).include('**/*.cpp')
})

project.Artifacts({                           // Defines your artifacts
	main: Manila.artifact(() => {             // Currently we only got a 'main' artifact
		Manila.job('clean')
			.description('Clean the client')
			.execute(() => {
				print('Cleaning Client...')
			})

		Manila.job('build').execute(() => {
			print('Building client...')       // This will be where we build your project
		})

		Manila.job('run')
			.description('Run the Client')
			.after('build')
			.execute(() => {
				print('Running client...')    // This will start your project
			})
	})
		.from('shiron.manila:cpp/console')
		.description('Client Main Artifact')
})
```

### 4\. The Workspace Script

Finally we'll add a global workspace-level script in the root of your workspace (`my-awesome-project/Manila.js`). It is used for setting build configurations that can be applied to each project.

```javascript
const workspace = Manila.getWorkspace()

Manila.onProject(['client', 'core'], p => {
    p.setToolChain(ToolChain.Clang)
})

```

### 5\. Build and Run\!

That's it\! Now you can build and run your project from the command line:

```bash
manila run client/main:run
```

This command follows the format `manila run <project-name>/<artifact-name>:<job-name>`. In our case, we are executing the *run* job defined in the *main* artifact of the *client* project.

You should see the output: `Hello World!`

-----

## Key Concepts

ManilaBuild is built around a few core concepts:

  * **Workspaces and Projects**: A workspace contains one or more projects, each with its own build configuration. This allows you to manage complex, multi-project solutions with ease.
  * **Jobs**: Jobs are the fundamental building blocks of your build process. You can define jobs for compiling, running, testing, or any other action you need to perform. Jobs can also have dependencies on other jobs.
  * **Plugins**: ManilaBuild is highly extensible through plugins. The C++ plugin, for example, provides components for building static libraries and console applications. You can even create your own plugins to add new functionality.
  * **JavaScript Configuration**: Build scripts are written in JavaScript, giving you the full power and flexibility of a real programming language for your build logic.

-----

## Dive Deeper

While the build system is still evolving rapidly, you can check out more examples inside the `run/` directory to see how new features are tested and implemented.
