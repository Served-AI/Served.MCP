using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Infrastructure Management Tools - Proxmox, Kubernetes, Docker monitoring.
/// Provides visibility into the Helledi cluster infrastructure.
/// </summary>
public static class InfrastructureTools
{
    /// <summary>
    /// Register all infrastructure monitoring tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        // ----- Cluster Health -----

        server.RegisterTool("GetClusterHealth",
            "Get overall health status of the Helledi cluster infrastructure.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/infrastructure/health");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get cluster health" };
                }

                return await response.Content.ReadAsStringAsync();
            });

        server.RegisterTool("GetNodeStatus",
            "Get status of all nodes in the cluster.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/infrastructure/nodes");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get node status" };
                }

                var nodes = JArray.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("# Cluster Nodes");
                sb.AppendLine();

                foreach (var node in nodes)
                {
                    var status = node["status"]?.ToString() == "online" ? "🟢" : "🔴";
                    sb.AppendLine($"## {status} {node["name"]}");
                    sb.AppendLine($"- **Type:** {node["type"]}");
                    sb.AppendLine($"- **CPU:** {node["cpuUsage"]:P0} | **Memory:** {node["memoryUsage"]:P0}");
                    sb.AppendLine($"- **Disk:** {node["diskUsage"]:P0}");
                    sb.AppendLine($"- **Uptime:** {node["uptime"]}");
                    sb.AppendLine();
                }

                return sb.ToString();
            });

        // ----- Kubernetes -----

        server.RegisterTool("GetKubernetesPods",
            "Get Kubernetes pods status. Filter by namespace.",
            async (args) =>
            {
                var ns = args["namespace"]?.Value<string>() ?? "served";
                var response = await server.Http.GetAsync($"/api/infrastructure/kubernetes/pods?namespace={ns}");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get pods" };
                }

                var pods = JArray.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"# Kubernetes Pods ({ns})");
                sb.AppendLine();

                foreach (var pod in pods)
                {
                    var phase = pod["phase"]?.ToString();
                    var emoji = phase == "Running" ? "🟢" : phase == "Pending" ? "🟡" : "🔴";
                    sb.AppendLine($"- {emoji} **{pod["name"]}** - {phase}");
                    sb.AppendLine($"  Restarts: {pod["restarts"]} | Age: {pod["age"]}");
                }

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["namespace"] = new JObject { ["type"] = "string", ["description"] = "Kubernetes namespace (default: served)" }
                }
            });

        server.RegisterTool("GetKubernetesDeployments",
            "Get Kubernetes deployments status.",
            async (args) =>
            {
                var ns = args["namespace"]?.Value<string>() ?? "served";
                var response = await server.Http.GetAsync($"/api/infrastructure/kubernetes/deployments?namespace={ns}");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get deployments" };
                }

                var deployments = JArray.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"# Kubernetes Deployments ({ns})");
                sb.AppendLine();

                foreach (var d in deployments)
                {
                    var ready = d["readyReplicas"]?.Value<int>() ?? 0;
                    var desired = d["replicas"]?.Value<int>() ?? 0;
                    var emoji = ready == desired ? "🟢" : "🟡";
                    sb.AppendLine($"- {emoji} **{d["name"]}** - {ready}/{desired} ready");
                    sb.AppendLine($"  Image: {d["image"]}");
                }

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["namespace"] = new JObject { ["type"] = "string", ["description"] = "Kubernetes namespace (default: served)" }
                }
            });

        server.RegisterTool("RestartDeployment",
            "Restart a Kubernetes deployment (rolling restart).",
            async (args) =>
            {
                var name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required");
                var ns = args["namespace"]?.Value<string>() ?? "served";

                var response = await server.Http.PostAsync(
                    $"/api/infrastructure/kubernetes/deployments/{name}/restart?namespace={ns}",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new { success = false, error };
                }

                return new { success = true, message = $"Deployment {name} restart initiated" };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject { ["type"] = "string", ["description"] = "Deployment name" },
                    ["namespace"] = new JObject { ["type"] = "string", ["description"] = "Kubernetes namespace (default: served)" }
                },
                ["required"] = new JArray { "name" }
            });

        // ----- Docker -----

        server.RegisterTool("GetDockerContainers",
            "Get Docker containers status on a specific node.",
            async (args) =>
            {
                var node = args["node"]?.Value<string>() ?? "served-dev";
                var response = await server.Http.GetAsync($"/api/infrastructure/docker/containers?node={node}");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get containers" };
                }

                var containers = JArray.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"# Docker Containers ({node})");
                sb.AppendLine();

                foreach (var c in containers)
                {
                    var state = c["state"]?.ToString();
                    var emoji = state == "running" ? "🟢" : "🔴";
                    sb.AppendLine($"- {emoji} **{c["name"]}** - {state}");
                    sb.AppendLine($"  Image: {c["image"]} | Ports: {c["ports"]}");
                }

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["node"] = new JObject { ["type"] = "string", ["description"] = "Node name (default: served-dev)" }
                }
            });

        // ----- Proxmox -----

        server.RegisterTool("GetProxmoxVMs",
            "Get Proxmox virtual machines status.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/infrastructure/proxmox/vms");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get VMs" };
                }

                var vms = JArray.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("# Proxmox Virtual Machines");
                sb.AppendLine();

                foreach (var vm in vms)
                {
                    var status = vm["status"]?.ToString();
                    var emoji = status == "running" ? "🟢" : "🔴";
                    sb.AppendLine($"- {emoji} **{vm["name"]}** (ID: {vm["vmid"]})");
                    sb.AppendLine($"  Status: {status} | CPU: {vm["cpu"]:P0} | Memory: {vm["mem"]:P0}");
                    sb.AppendLine($"  Disk: {vm["disk"]} | Node: {vm["node"]}");
                    sb.AppendLine();
                }

                return sb.ToString();
            });

        // ----- Metrics -----

        server.RegisterTool("GetResourceMetrics",
            "Get resource usage metrics (CPU, memory, disk, network).",
            async (args) =>
            {
                var target = args["target"]?.Value<string>() ?? "cluster";
                var period = args["period"]?.Value<string>() ?? "1h";

                var response = await server.Http.GetAsync($"/api/infrastructure/metrics?target={target}&period={period}");

                if (!response.IsSuccessStatusCode)
                {
                    return new { success = false, error = "Failed to get metrics" };
                }

                return await response.Content.ReadAsStringAsync();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["target"] = new JObject { ["type"] = "string", ["description"] = "Target: cluster, node name, or pod name" },
                    ["period"] = new JObject { ["type"] = "string", ["description"] = "Time period: 1h, 6h, 24h, 7d (default: 1h)" }
                }
            });
    }
}
