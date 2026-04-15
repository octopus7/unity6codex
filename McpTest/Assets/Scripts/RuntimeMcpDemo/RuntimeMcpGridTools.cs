#nullable enable

using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace McpTest.RuntimeMcpDemo
{
    [McpPluginToolType]
    public static class RuntimeMcpGridTools
    {
        public const string GetStateToolId = "runtime-grid-get-state";
        public const string GetLegalMovesToolId = "runtime-grid-get-legal-moves";
        public const string MoveToolId = "runtime-grid-move";
        public const string ResetToolId = "runtime-grid-reset";

        [McpPluginTool(
            GetStateToolId,
            Title = "Runtime Grid / Get State",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description("Returns the current state of the live runtime grid arena. Call this before deciding the next move.")]
        public static RuntimeGridState GetState(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                if (!Application.isPlaying)
                {
                    return NotPlayingState();
                }

                return RuntimeMcpGridDemo.Instance.GetState();
            });
        }

        [McpPluginTool(
            GetLegalMovesToolId,
            Title = "Runtime Grid / Get Legal Moves",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description("Returns the currently legal one-step moves for the live runtime grid arena.")]
        public static string[] GetLegalMoves(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                if (!Application.isPlaying)
                {
                    return System.Array.Empty<string>();
                }

                return RuntimeMcpGridDemo.Instance.GetLegalMoves();
            });
        }

        [McpPluginTool(
            MoveToolId,
            Title = "Runtime Grid / Move")]
        [Description("Moves the live runtime agent one cell in the requested cardinal direction.")]
        public static RuntimeGridMoveResult Move(
            [Description("One of up, right, down, left. The aliases north, east, south, and west are also accepted.")]
            string direction)
        {
            return MainThread.Instance.Run(() =>
            {
                if (!Application.isPlaying)
                {
                    return new RuntimeGridMoveResult
                    {
                        Accepted = false,
                        Message = "Enter Play mode to use runtime-grid-move.",
                        State = NotPlayingState()
                    };
                }

                return RuntimeMcpGridDemo.Instance.Move(direction);
            });
        }

        [McpPluginTool(
            ResetToolId,
            Title = "Runtime Grid / Reset")]
        [Description("Resets the live runtime grid arena to its starting state.")]
        public static RuntimeGridState Reset(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                if (!Application.isPlaying)
                {
                    return NotPlayingState();
                }

                return RuntimeMcpGridDemo.Instance.ResetArena();
            });
        }

        static RuntimeGridState NotPlayingState()
        {
            return new RuntimeGridState
            {
                GridSize = RuntimeGridModel.DefaultGridSize,
                Agent = new GridCell(0, 0),
                Goal = new GridCell(4, 4),
                Obstacles = new[]
                {
                    new GridCell(2, 1),
                    new GridCell(2, 2),
                    new GridCell(1, 3)
                },
                LegalMoves = System.Array.Empty<string>(),
                StepCount = 0,
                HasReachedGoal = false,
                LastResult = "Enter Play mode to use the live runtime grid tools.",
                ConnectionStatus = "Runtime MCP demo is idle because the editor is not in Play mode.",
                Summary = "Runtime grid tools are only live during Play mode."
            };
        }
    }

    [McpPluginPromptType]
    public static class RuntimeMcpGridPrompt
    {
        [McpPluginPrompt(Name = "runtime-grid-demo-guide", Role = Role.User)]
        [Description("Explains how to control the live runtime grid demo through MCP tools.")]
        public static string Guide()
        {
            return
                "You are controlling a live Unity play-mode demo. " +
                "Always call runtime-grid-get-state first, prefer runtime-grid-get-legal-moves before runtime-grid-move, " +
                "move one step at a time, and stop once the agent reaches the goal.";
        }
    }
}
