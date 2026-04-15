#nullable enable

using NUnit.Framework;

namespace McpTest.RuntimeMcpDemo.Tests
{
    public sealed class RuntimeGridModelTests
    {
        [Test]
        public void Reset_CreatesExpectedArenaState()
        {
            var model = new RuntimeGridModel();

            var state = model.CreateState("test");

            Assert.That(state.GridSize, Is.EqualTo(RuntimeGridModel.DefaultGridSize));
            Assert.That(state.Agent.X, Is.EqualTo(0));
            Assert.That(state.Agent.Y, Is.EqualTo(0));
            Assert.That(state.Goal.X, Is.EqualTo(4));
            Assert.That(state.Goal.Y, Is.EqualTo(4));
            Assert.That(state.Obstacles, Has.Length.EqualTo(3));
        }

        [Test]
        public void Move_RejectsBlockedCell()
        {
            var model = new RuntimeGridModel();

            Assert.That(model.Move("right", "test").Accepted, Is.True);
            Assert.That(model.Move("up", "test").Accepted, Is.True);

            var result = model.Move("right", "test");

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.State.Agent.X, Is.EqualTo(1));
            Assert.That(result.State.Agent.Y, Is.EqualTo(1));
        }

        [Test]
        public void Move_ReachesGoalOnValidPath()
        {
            var model = new RuntimeGridModel();

            var path = new[] { "right", "right", "right", "right", "up", "up", "up", "up" };
            RuntimeGridMoveResult result = new RuntimeGridMoveResult();

            foreach (var move in path)
            {
                result = model.Move(move, "test");
                Assert.That(result.Accepted, Is.True, "Expected move to succeed: " + move);
            }

            Assert.That(result.State.HasReachedGoal, Is.True);
            Assert.That(result.State.Agent.X, Is.EqualTo(4));
            Assert.That(result.State.Agent.Y, Is.EqualTo(4));
        }
    }
}
