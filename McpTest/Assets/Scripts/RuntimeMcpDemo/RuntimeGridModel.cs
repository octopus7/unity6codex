#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace McpTest.RuntimeMcpDemo
{
    [Serializable]
    public sealed class GridCell
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridCell()
        {
        }

        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [Serializable]
    public sealed class RuntimeGridState
    {
        public int GridSize { get; set; }
        public GridCell Agent { get; set; } = new GridCell();
        public GridCell Goal { get; set; } = new GridCell();
        public GridCell[] Obstacles { get; set; } = Array.Empty<GridCell>();
        public string[] LegalMoves { get; set; } = Array.Empty<string>();
        public int StepCount { get; set; }
        public bool HasReachedGoal { get; set; }
        public string LastResult { get; set; } = string.Empty;
        public string ConnectionStatus { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class RuntimeGridMoveResult
    {
        public bool Accepted { get; set; }
        public string Message { get; set; } = string.Empty;
        public RuntimeGridState State { get; set; } = new RuntimeGridState();
    }

    public sealed class RuntimeGridModel
    {
        public const int DefaultGridSize = 5;

        static readonly (int x, int y)[] DefaultObstacles =
        {
            (2, 1),
            (2, 2),
            (1, 3)
        };

        readonly HashSet<(int x, int y)> _blockedCells = new HashSet<(int x, int y)>();

        GridCell _agent = new GridCell();
        GridCell _goal = new GridCell();
        int _stepCount;
        string _lastResult = "Call runtime-grid-get-state to inspect the live arena.";

        public RuntimeGridModel(int gridSize = DefaultGridSize)
        {
            GridSize = gridSize;
            Reset();
        }

        public int GridSize { get; }

        public IReadOnlyCollection<(int x, int y)> Obstacles => _blockedCells;

        public GridCell Agent => new GridCell(_agent.X, _agent.Y);

        public GridCell Goal => new GridCell(_goal.X, _goal.Y);

        public int StepCount => _stepCount;

        public string LastResult => _lastResult;

        public bool HasReachedGoal => _agent.X == _goal.X && _agent.Y == _goal.Y;

        public void Reset()
        {
            _blockedCells.Clear();
            foreach (var obstacle in DefaultObstacles)
            {
                _blockedCells.Add(obstacle);
            }

            _agent = new GridCell(0, 0);
            _goal = new GridCell(GridSize - 1, GridSize - 1);
            _stepCount = 0;
            _lastResult = "Arena reset. Reach the goal in the top-right corner.";
        }

        public string[] GetLegalMoves()
        {
            var moves = new List<string>();

            TryAddMove("up", 0, 1, moves);
            TryAddMove("right", 1, 0, moves);
            TryAddMove("down", 0, -1, moves);
            TryAddMove("left", -1, 0, moves);

            return moves.ToArray();
        }

        public RuntimeGridMoveResult Move(string? direction, string connectionStatus)
        {
            if (!TryNormalizeDirection(direction, out var normalizedDirection, out var deltaX, out var deltaY))
            {
                return Reject(
                    "Unsupported direction. Use up, right, down, left, or their north/east/south/west aliases.",
                    connectionStatus);
            }

            var next = (_agent.X + deltaX, _agent.Y + deltaY);
            if (!IsInside(next))
            {
                return Reject(
                    "Move rejected because it would leave the arena bounds.",
                    connectionStatus);
            }

            if (_blockedCells.Contains(next))
            {
                return Reject(
                    "Move rejected because that cell contains an obstacle.",
                    connectionStatus);
            }

            _agent = new GridCell(next.Item1, next.Item2);
            _stepCount++;

            _lastResult = HasReachedGoal
                ? "Move accepted. Goal reached."
                : "Move accepted. Continue toward the goal.";

            return new RuntimeGridMoveResult
            {
                Accepted = true,
                Message = "Moved " + normalizedDirection + ".",
                State = CreateState(connectionStatus)
            };
        }

        public RuntimeGridState CreateState(string connectionStatus)
        {
            var legalMoves = GetLegalMoves();
            var reachedGoal = HasReachedGoal;

            return new RuntimeGridState
            {
                GridSize = GridSize,
                Agent = Agent,
                Goal = Goal,
                Obstacles = _blockedCells
                    .Select(cell => new GridCell(cell.x, cell.y))
                    .ToArray(),
                LegalMoves = legalMoves,
                StepCount = _stepCount,
                HasReachedGoal = reachedGoal,
                LastResult = _lastResult,
                ConnectionStatus = connectionStatus,
                Summary = BuildSummary(legalMoves, reachedGoal, connectionStatus)
            };
        }

        RuntimeGridMoveResult Reject(string message, string connectionStatus)
        {
            _lastResult = message;

            return new RuntimeGridMoveResult
            {
                Accepted = false,
                Message = message,
                State = CreateState(connectionStatus)
            };
        }

        void TryAddMove(string direction, int deltaX, int deltaY, ICollection<string> result)
        {
            var candidate = (_agent.X + deltaX, _agent.Y + deltaY);
            if (IsInside(candidate) && !_blockedCells.Contains(candidate))
            {
                result.Add(direction);
            }
        }

        bool IsInside((int x, int y) cell)
        {
            return cell.x >= 0 && cell.x < GridSize && cell.y >= 0 && cell.y < GridSize;
        }

        static bool TryNormalizeDirection(
            string? direction,
            out string normalizedDirection,
            out int deltaX,
            out int deltaY)
        {
            normalizedDirection = string.Empty;
            deltaX = 0;
            deltaY = 0;

            if (string.IsNullOrWhiteSpace(direction))
            {
                return false;
            }

            switch (direction.Trim().ToLowerInvariant())
            {
                case "up":
                case "north":
                    normalizedDirection = "up";
                    deltaY = 1;
                    return true;

                case "right":
                case "east":
                    normalizedDirection = "right";
                    deltaX = 1;
                    return true;

                case "down":
                case "south":
                    normalizedDirection = "down";
                    deltaY = -1;
                    return true;

                case "left":
                case "west":
                    normalizedDirection = "left";
                    deltaX = -1;
                    return true;

                default:
                    return false;
            }
        }

        string BuildSummary(string[] legalMoves, bool reachedGoal, string connectionStatus)
        {
            var legalMovesText = legalMoves.Length > 0 ? string.Join(", ", legalMoves) : "none";

            return string.Format(
                "Live runtime grid demo. Agent=({0},{1}), Goal=({2},{3}), LegalMoves=[{4}], Steps={5}, GoalReached={6}, Connection='{7}'.",
                _agent.X,
                _agent.Y,
                _goal.X,
                _goal.Y,
                legalMovesText,
                _stepCount,
                reachedGoal,
                connectionStatus);
        }
    }
}
