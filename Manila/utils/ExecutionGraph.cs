using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiron.Manila.Utils;

/// <summary>
/// Represents a base class for any object that can be executed within a graph.
/// </summary>
public abstract class ExecutableObject {
    // The ID is cached to ensure it's consistent for each object instance.
    public Guid ExecutableID { get; } = Guid.NewGuid();

    public virtual bool IsBlocking() { return true; }

    // Returns the cached ID for the object.
    public virtual string GetID() { return ExecutableID.ToString(); }

    protected virtual void PreRun() { }
    protected abstract void Run();
    protected virtual void PostRun() { }

    public void Execute() {
        PreRun();

        if (IsBlocking()) {
            Run();
        } else {
            Task.Run(Run);
        }

        PostRun();
    }

    /// <summary>
    /// Provides a string representation of the executable object.
    /// </summary>
    /// <returns>The type name and the first 8 characters of its unique ID.</returns>
    public override string ToString() {
        return $"{GetType().Name} [{GetID().AsSpan(0, 8)}]";
    }

    public override bool Equals(object? obj) {
        if (obj is ExecutableObject o) return o.GetID() == GetID();
        return false;
    }

    /// <summary>
    /// Gets the hash code for the object, based on its unique ID.
    /// This is crucial for performance when the object is used as a key in a dictionary.
    /// </summary>
    /// <returns>The hash code of the object's ID.</returns>
    public override int GetHashCode() {
        return GetID().GetHashCode();
    }
}

/// <summary>
/// A concrete implementation of an <see cref="ExecutableObject"/> that performs no operation.
/// </summary>
public class NoOpExecutableObject : ExecutableObject {
    public override bool IsBlocking() { return true; }
    protected override void Run() { }
}

/// <summary>
/// Represents a directed acyclic graph of executable objects, capable of resolving
/// complex dependency chains and calculating parallel execution layers.
/// </summary>
public class ExecutionGraph {
    /// <summary>
    /// Represents a node within the <see cref="ExecutionGraph"/>, containing an <see cref="ExecutableObject"/>
    /// and its relationships to other nodes.
    /// </summary>
    /// <param name="obj">The executable object this node represents.</param>
    /// <param name="children">A list of nodes that depend on this node.</param>
    /// <param name="parents">A list of nodes that this node depends on.</param>
    public class ExecutionNode(ExecutableObject obj, List<ExecutionNode>? children = null, List<ExecutionNode>? parents = null) {
        public ExecutableObject ExecutableObject { get; } = obj;
        public List<ExecutionNode> Children { get; } = children ?? [];
        public List<ExecutionNode> Parents { get; } = parents ?? [];

        /// <summary>
        /// Delegates the string representation to the contained ExecutableObject.
        /// </summary>
        public override string ToString() => ExecutableObject.ToString();
    }
    public class ExecutionLayer(ExecutableObject[] items) {
        public readonly ExecutableObject[] Items = items;
    }

    /// <summary>
    /// A dictionary to efficiently store and retrieve all nodes in the graph,
    /// using the ExecutableObject as the key.
    /// </summary>
    private readonly Dictionary<ExecutableObject, ExecutionNode> _nodes = new();

    /// <summary>
    /// Retrieves an existing ExecutionNode for a given object or creates a new one if it doesn't exist.
    /// </summary>
    private ExecutionNode GetOrCreateNode(ExecutableObject obj) {
        if (!_nodes.TryGetValue(obj, out var node)) {
            node = new ExecutionNode(obj);
            _nodes[obj] = node;
        }
        return node;
    }

    /// <summary>
    /// Recursively finds all unique descendants (children, grandchildren, etc.) of a given node.
    /// </summary>
    private void GetAllDescendants(ExecutionNode startNode, HashSet<ExecutionNode> descendants) {
        foreach (var child in startNode.Children) {
            if (descendants.Add(child)) {
                GetAllDescendants(child, descendants);
            }
        }
    }

    /// <summary>
    /// Recursively finds all unique ancestors (parents, grandparents, etc.) of a given node.
    /// </summary>
    private void GetAllAncestors(ExecutionNode startNode, HashSet<ExecutionNode> ancestors) {
        foreach (var parent in startNode.Parents) {
            if (ancestors.Add(parent)) {
                GetAllAncestors(parent, ancestors);
            }
        }
    }

    /// <summary>
    /// Attaches an object and its direct dependencies to the graph. This method correctly
    /// resolves and updates both direct and transitive parent/child relationships.
    /// </summary>
    /// <param name="main">The main object to be added or updated in the graph.</param>
    /// <param name="dependencies">A list of objects that the main object directly depends on.</param>
    public void Attach(ExecutableObject main, List<ExecutableObject> dependencies) {
        var mainNode = GetOrCreateNode(main);

        var allDescendants = new HashSet<ExecutionNode>();
        GetAllDescendants(mainNode, allDescendants);
        allDescendants.Add(mainNode);

        foreach (var dep in dependencies) {
            var dependencyNode = GetOrCreateNode(dep);

            if (!mainNode.Parents.Contains(dependencyNode)) {
                mainNode.Parents.Add(dependencyNode);
            }

            var allAncestors = new HashSet<ExecutionNode>();
            GetAllAncestors(dependencyNode, allAncestors);
            allAncestors.Add(dependencyNode);

            foreach (var ancestor in allAncestors) {
                foreach (var descendant in allDescendants) {
                    if (!ancestor.Children.Contains(descendant)) {
                        ancestor.Children.Add(descendant);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates a string representation of the entire execution graph,
    /// detailing each node and its parent/child relationships.
    /// </summary>
    /// <returns>A formatted string visualizing the graph.</returns>
    public override string ToString() {
        var sb = new StringBuilder();
        sb.AppendLine("Execution Graph:");
        sb.AppendLine("--------------------");

        if (_nodes.Count == 0) {
            sb.AppendLine("Graph is empty.");
            return sb.ToString();
        }

        foreach (var node in _nodes.Values) {
            sb.AppendLine($"Node: {node}");

            sb.Append("  Parents: ");
            sb.AppendLine(node.Parents.Count == 0 ? "None" : string.Join(", ", node.Parents));

            sb.Append("  Children: ");
            sb.AppendLine(node.Children.Count == 0 ? "None" : string.Join(", ", node.Children));

            sb.AppendLine("--------------------");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Mermaid diagram string for the graph's direct dependencies.
    /// This visualizes the direct parent-to-child relationships.
    /// </summary>
    /// <returns>A string formatted for use with Mermaid.js.</returns>
    public string ToMermaid() {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD"); // TD = Top Down

        if (_nodes.Count == 0) {
            return sb.ToString();
        }

        var definedLinks = new HashSet<string>();

        foreach (var childNode in _nodes.Values) {
            if (childNode.Parents.Count == 0 && childNode.Children.Count == 0) {
                sb.AppendLine($"  id{childNode.ExecutableObject.GetHashCode()}[\"{childNode.ExecutableObject}\"]");
                continue;
            }

            foreach (var parentNode in childNode.Parents) {
                string link = $"id{parentNode.ExecutableObject.GetHashCode()} --> id{childNode.ExecutableObject.GetHashCode()}";
                if (definedLinks.Add(link)) {
                    sb.AppendLine($"  id{parentNode.ExecutableObject.GetHashCode()}[\"{parentNode.ExecutableObject}\"] --> id{childNode.ExecutableObject.GetHashCode()}[\"{childNode.ExecutableObject}\"]");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Finds a node in the graph associated with a specific task identifier.
    /// Note: This requires the ExecutableObject to be of a specific 'API.Task' type.
    /// </summary>
    /// <param name="taskID">The unique identifier of the task to find.</param>
    /// <returns>The corresponding <see cref="ExecutionNode"/>, or null if not found.</returns>
    public ExecutionNode? GetByTask(string taskID) {
        foreach (var o in _nodes.Keys) {
            if (o is API.Task task) {
                if (task.GetIdentifier().Equals(taskID)) return _nodes[o];
            }
        }
        return null;
    }

    /// <summary>
    /// Calculates the execution layers for a given task using a topological sort.
    /// Each layer contains nodes that can be run in parallel. The calculation is based on the
    /// subgraph containing only the target task and its direct and indirect dependencies.
    /// </summary>
    /// <param name="task">The identifier of the final task to be executed.</param>
    /// <returns>An array of arrays, where each inner array is a layer of <see cref="ExecutionNode"/> that can be run in parallel.</returns>
    /// <exception cref="Exception">Thrown if the specified task is not found in the graph.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a cycle is detected in the dependency graph for the task.</exception>
    public ExecutionLayer[] GetExecutionLayers(string task) {
        var targetNode = GetByTask(task);
        if (targetNode == null) throw new Exception($"Task '{task}' not inside graph!");

        var subgraphNodes = new HashSet<ExecutionNode> {
            targetNode
        };
        GetAllAncestors(targetNode, subgraphNodes);

        var directChildrenMap = subgraphNodes.ToDictionary(n => n, n => new List<ExecutionNode>());
        foreach (var node in subgraphNodes) {
            foreach (var parent in node.Parents) {
                if (directChildrenMap.ContainsKey(parent)) {
                    directChildrenMap[parent].Add(node);
                }
            }
        }

        var inDegrees = subgraphNodes.ToDictionary(
            node => node,
            node => node.Parents.Count(parent => subgraphNodes.Contains(parent))
        );

        var queue = new Queue<ExecutionNode>();
        foreach (var node in subgraphNodes) {
            if (inDegrees[node] == 0) {
                queue.Enqueue(node);
            }
        }

        var allLayers = new List<List<ExecutionNode>>();
        int processedNodesCount = 0;
        while (queue.Count > 0) {
            var currentLayer = new List<ExecutionNode>();
            int layerSize = queue.Count;

            for (int i = 0; i < layerSize; i++) {
                var node = queue.Dequeue();
                currentLayer.Add(node);
                processedNodesCount++;

                foreach (var child in directChildrenMap[node]) {
                    inDegrees[child]--;
                    if (inDegrees[child] == 0) {
                        queue.Enqueue(child);
                    }
                }
            }
            allLayers.Add(currentLayer);
        }

        if (processedNodesCount != subgraphNodes.Count) {
            throw new InvalidOperationException("A cycle was detected in the dependency graph for the given task.");
        }

        return allLayers
            .Select(layer => new ExecutionLayer(layer.Select(node => node.ExecutableObject).ToArray()))
            .ToArray();
    }
}
