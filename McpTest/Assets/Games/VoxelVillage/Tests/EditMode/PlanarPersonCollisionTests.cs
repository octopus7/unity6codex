#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class PlanarPersonCollisionTests
    {
        [Test]
        public void Resolve_ClampsDesiredPosition_WhenEnteringVillagerRadius()
        {
            var current = new Vector2(0f, -2f);
            var desired = new Vector2(0f, -0.2f);
            var obstacle = Vector2.zero;

            var resolved = PlanarPersonCollision.Resolve(current, desired, obstacle, 1.4f);

            Assert.That(Vector2.Distance(resolved, obstacle), Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(resolved.y, Is.LessThan(0f));
        }

        [Test]
        public void Resolve_LeavesDesiredPositionUntouched_WhenOutsideVillagerRadius()
        {
            var current = new Vector2(-3f, -3f);
            var desired = new Vector2(-2f, -2f);
            var obstacle = Vector2.zero;

            var resolved = PlanarPersonCollision.Resolve(current, desired, obstacle, 1.4f);

            Assert.AreEqual(desired, resolved);
        }
    }
}
