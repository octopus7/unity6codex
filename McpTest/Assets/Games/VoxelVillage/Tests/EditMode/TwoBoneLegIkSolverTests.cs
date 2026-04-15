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

        [Test]
        public void SolveWithHint_BendsKneeUpwardForMSilhouette()
        {
            var pose = TwoBoneLegIkSolver.SolveWithHint(
                new Vector3(0f, 0.7f, 0f),
                new Vector3(1.2f, -1f, 0.1f),
                new Vector3(1.35f, 1.9f, 0.2f),
                1.6f,
                1.9f);

            Assert.That(pose.KneePosition.y, Is.GreaterThan(0.7f));
            Assert.That(pose.KneePosition.y, Is.GreaterThan(pose.TargetPosition.y));
        }
    }
}
