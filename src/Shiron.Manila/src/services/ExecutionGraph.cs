using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shiron.Logging;
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Exceptions;
using Shiron.Profiling;

namespace Shiron.Manila.Services;

/// <summary>No-op executable placeholder.</summary>
public class NoOpExecutableObject : ExecutableObject {
    public override bool IsBlocking() { return true; }
    public override async Task RunAsync() {
        await Task.Yield();
    }
}

/// <summary>Execution DAG for jobs.</summary>
public class ExecutionGraph(ILogger logger, IProfiler profiler) {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;

    /// <summary>Graph node (object + relations).</summary>
    public class ExecutionNode(ExecutableObject obj, List<ExecutionNode>? children = null, List<ExecutionNode>? parents = null) {
        public ExecutableObject ExecutableObject { get; } = obj;
        public List<ExecutionNode> Children { get; } = children ?? [];
        public List<ExecutionNode> Parents { get; } = parents ?? [];

        /// <summary>Delegates to object.</summary>
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

    /// <summary>Get or create node.</summary>
    private ExecutionNode GetOrCreateNode(ExecutableObject obj) {
        if (!_nodes.TryGetValue(obj, out var node)) {
            node = new ExecutionNode(obj);
            _nodes[obj] = node;
        }
        return node;
    }

    /// <summary>Collect all descendants.</summary>
    private void GetAllDescendants(ExecutionNode startNode, HashSet<ExecutionNode> descendants) {
        foreach (var child in startNode.Children) {
            if (descendants.Add(child)) {
                GetAllDescendants(child, descendants);
            }
        }
    }

    /// <summary>Collect all ancestors.</summary>
    private void GetAllAncestors(ExecutionNode startNode, HashSet<ExecutionNode> ancestors) {
        foreach (var parent in startNode.Parents) {
            if (ancestors.Add(parent)) {
                GetAllAncestors(parent, ancestors);
            }
        }
    }

    /// <summary>Attach object and dependencies.</summary>
    /// <param name="main">Primary executable.</param>
    /// <param name="dependencies">Direct dependencies.</param>
    public void Attach(ExecutableObject main, List<ExecutableObject> dependencies) {
        _logger.Debug($"Attaching {((Job) main).GetIdentifier()}");

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

    /// <summary>Debug string dump.</summary>
    /// <returns>Graph formatted.</returns>
    public override string ToString() {
        var sb = new StringBuilder()
            .AppendLine("Execution Graph:")
            .AppendLine("--------------------");

        if (_nodes.Count == 0) {
            return sb.AppendLine("Graph is Empty").ToString();
        }

        foreach (var node in _nodes.Values) {
            _ = sb.AppendLine($"Node: {node}")
                .Append("  Parents: ")
                .AppendLine(node.Parents.Count == 0 ? "None" : string.Join(", ", node.Parents))

                .Append("  Children: ")
                .AppendLine(node.Children.Count == 0 ? "None" : string.Join(", ", node.Children))

                .AppendLine("--------------------");
        }

        return sb.ToString();
    }

    /// <summary>Mermaid graph text.</summary>
    public string ToMermaid() {
        var sb = new StringBuilder()
            .AppendLine("graph TD"); // TD = Top Down

        if (_nodes.Count == 0) {
            return sb.ToString();
        }

        var definedLinks = new HashSet<string>();

        foreach (var childNode in _nodes.Values) {
            if (childNode.Parents.Count == 0 && childNode.Children.Count == 0) {
                _ = sb.AppendLine($"  id{childNode.ExecutableObject.GetHashCode()}[\"{childNode.ExecutableObject}\"]");
                continue;
            }

            foreach (var parentNode in childNode.Parents) {
                string link = $"id{parentNode.ExecutableObject.GetHashCode()} --> id{childNode.ExecutableObject.GetHashCode()}";
                if (definedLinks.Add(link)) {
                    _ = sb.AppendLine($"  id{parentNode.ExecutableObject.GetHashCode()}[\"{parentNode.ExecutableObject}\"] --> id{childNode.ExecutableObject.GetHashCode()}[\"{childNode.ExecutableObject}\"]");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>Find node by job id.</summary>
    /// <returns>Node or null.</returns>
    public ExecutionNode? GetByJob(string jobID) {
        foreach (var o in _nodes.Keys) {
            if (o is Job job) {
                if (job.GetIdentifier() == jobID) return _nodes[o];
            }
        }
        return null;
    }

    /// <summary>Topological parallel layers for job.</summary>
    /// <param name="job">Final job id.</param>
    /// <returns>Ordered layers.</returns>
    public ExecutionLayer[] GetExecutionLayers(string job) {
        var targetNode = GetByJob(job) ?? throw new ManilaException($"Job '{job}' not inside graph!");
        var subgraphNodes = new HashSet<ExecutionNode> {
            targetNode
        };
        GetAllAncestors(targetNode, subgraphNodes);

        var directChildrenMap = subgraphNodes.ToDictionary(n => n, n => new List<ExecutionNode>());
        foreach (var node in subgraphNodes) {
            foreach (var parent in node.Parents) {
                if (directChildrenMap.TryGetValue(parent, out List<ExecutionNode>? value)) {
                    value.Add(node);
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
            throw new ManilaException("A cycle was detected in the dependency graph for the given job.");
        }

        return allLayers
            .Select(layer => new ExecutionLayer(layer.Select(node => node.ExecutableObject).ToArray()))
            .ToArray();
    }
}
