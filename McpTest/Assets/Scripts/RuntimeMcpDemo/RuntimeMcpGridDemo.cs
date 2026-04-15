#nullable enable

using System.Collections.Generic;
using com.IvanMurzak.Unity.MCP;
using UnityEngine;

namespace McpTest.RuntimeMcpDemo
{
    [DisallowMultipleComponent]
    public sealed class RuntimeMcpGridDemo : MonoBehaviour
    {
        const float CellSpacing = 2f;
        const float CellHeight = 0.5f;

        static RuntimeMcpGridDemo? _instance;

        readonly List<Transform> _obstacleTransforms = new List<Transform>();
        readonly RuntimeGridModel _model = new RuntimeGridModel();

        string _connectionStatus = "Runtime MCP not initialized.";

        GameObject _arenaRoot = null!;
        Transform _agentTransform = null!;
        Transform _goalTransform = null!;
        Renderer _goalRenderer = null!;

        public static RuntimeMcpGridDemo Instance => EnsureExists();

        public static RuntimeMcpGridDemo EnsureExists()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindAnyObjectByType<RuntimeMcpGridDemo>();
            if (_instance != null)
            {
                return _instance;
            }

            var root = new GameObject(nameof(RuntimeMcpGridDemo));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(root);
            }
            _instance = root.AddComponent<RuntimeMcpGridDemo>();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureVisuals();
            SyncVisuals();
        }

        void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            if (UnityMcpPluginRuntime.HasInstance)
            {
                UnityMcpPluginRuntime.DisposeInstance();
            }

            _instance = null;
        }

        public void SetConnectionStatus(string status)
        {
            _connectionStatus = status;
        }

        public RuntimeGridState GetState()
        {
            return _model.CreateState(_connectionStatus);
        }

        public string[] GetLegalMoves()
        {
            return _model.GetLegalMoves();
        }

        public RuntimeGridMoveResult Move(string direction)
        {
            var result = _model.Move(direction, _connectionStatus);
            SyncVisuals();
            return result;
        }

        public RuntimeGridState ResetArena()
        {
            _model.Reset();
            SyncVisuals();
            return GetState();
        }

        void EnsureVisuals()
        {
            if (_arenaRoot != null)
            {
                return;
            }

            _arenaRoot = new GameObject("Arena");
            _arenaRoot.transform.SetParent(transform, false);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(_arenaRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            var floorSize = (_model.GridSize - 1) * CellSpacing + 1.6f;
            floor.transform.localScale = new Vector3(floorSize, 0.2f, floorSize);
            floor.GetComponent<Renderer>().material.color = new Color(0.16f, 0.19f, 0.24f);

            var agent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            agent.name = "Agent";
            agent.transform.SetParent(_arenaRoot.transform, false);
            agent.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            agent.GetComponent<Renderer>().material.color = new Color(0.93f, 0.45f, 0.12f);
            _agentTransform = agent.transform;

            var goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goal.name = "Goal";
            goal.transform.SetParent(_arenaRoot.transform, false);
            goal.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            _goalTransform = goal.transform;
            _goalRenderer = goal.GetComponent<Renderer>();

            foreach (var obstacleCell in _model.Obstacles)
            {
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Obstacle_" + obstacleCell.x + "_" + obstacleCell.y;
                obstacle.transform.SetParent(_arenaRoot.transform, false);
                obstacle.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                obstacle.GetComponent<Renderer>().material.color = new Color(0.38f, 0.42f, 0.48f);
                _obstacleTransforms.Add(obstacle.transform);
            }
        }

        void SyncVisuals()
        {
            EnsureVisuals();

            var state = _model.CreateState(_connectionStatus);

            _agentTransform.position = CellToWorld(state.Agent);
            _goalTransform.position = CellToWorld(state.Goal) + new Vector3(0f, -0.1f, 0f);
            _goalRenderer.material.color = state.HasReachedGoal
                ? new Color(0.96f, 0.8f, 0.2f)
                : new Color(0.14f, 0.75f, 0.36f);

            var index = 0;
            foreach (var obstacleCell in _model.Obstacles)
            {
                _obstacleTransforms[index].position = CellToWorld(new GridCell(obstacleCell.x, obstacleCell.y));
                index++;
            }
        }

        Vector3 CellToWorld(GridCell cell)
        {
            var centerOffset = (_model.GridSize - 1) * 0.5f;
            return new Vector3(
                (cell.X - centerOffset) * CellSpacing,
                CellHeight,
                (cell.Y - centerOffset) * CellSpacing);
        }

        void OnGUI()
        {
            var state = GetState();

            GUILayout.BeginArea(new Rect(12f, 12f, 460f, 220f), GUI.skin.box);
            GUILayout.Label("Runtime MCP Grid Demo");
            GUILayout.Label("Play mode only. External LLM calls the runtime-grid-* tools while the game is running.");
            GUILayout.Label("Connection: " + state.ConnectionStatus);
            GUILayout.Label("Agent: (" + state.Agent.X + ", " + state.Agent.Y + ")  Goal: (" + state.Goal.X + ", " + state.Goal.Y + ")");
            GUILayout.Label("Legal moves: " + string.Join(", ", state.LegalMoves));
            GUILayout.Label("Steps: " + state.StepCount + "  Goal reached: " + state.HasReachedGoal);
            GUILayout.Label("Last result: " + state.LastResult);

            if (GUILayout.Button("Reset Arena"))
            {
                ResetArena();
            }

            GUILayout.EndArea();
        }
    }
}
