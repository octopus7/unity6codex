#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class TwoBoneLegIkSolverTests
    {
        [Test]
        public void Solve_ClampsTargetToMaximumReach()
        {
            var pose = TwoBoneLegIkSolver.Solve(
                Vector3.zero,
                new Vector3(3f, 0f, 0f),
                Vector3.forward,
                1f,
                1f);

            Assert.That(pose.ClampedDistance, Is.LessThanOrEqualTo(2f));
            Assert.That(pose.TargetPosition.magnitude, Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void Solve_PreservesMirrorDirectionAcrossLeftAndRightTargets()
        {
            var left = TwoBoneLegIkSolver.Solve(
                Vector3.zero,
                new Vector3(-1.1f, -0.5f, 0.8f),
                Vector3.forward,
                1f,
                1f);

            var right = TwoBoneLegIkSolver.Solve(
                Vector3.zero,
                new Vector3(1.1f, -0.5f, 0.8f),
                Vector3.forward,
                1f,
                1f);

            Assert.That(left.KneePosition.x, Is.LessThan(0f));
            Assert.That(right.KneePosition.x, Is.GreaterThan(0f));
        }
    }
}
