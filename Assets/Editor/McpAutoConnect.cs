using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Connects the MCP for Unity bridge to a local MCP server automatically after every domain reload, so an AI
/// client (Claude Code, see .mcp.json) can drive the Editor without anyone opening the MCP window and pressing
/// Start. Does nothing when no server is reachable on the configured local URL, or when the bridge already runs.
/// Lives in Assembly-CSharp-Editor because MCP for Unity's editor assembly is auto-referenced only there.
/// </summary>
[InitializeOnLoad]
internal static class McpAutoConnect
{
    static McpAutoConnect()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += TryConnect;
    }

    private static async void TryConnect()
    {
        try
        {
            if (MCPServiceLocator.Bridge.IsRunning) return;
            if (!MCPServiceLocator.Server.IsLocalHttpServerReachable()) return;
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            Debug.Log($"McpAutoConnect: MCP bridge {(started ? "connected" : "failed to connect")} to the local server.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"McpAutoConnect: {ex.Message}");
        }
    }
}
