using NUnit.Framework;
using CodexSix.RequestPipeline.Editor;

namespace CodexSix.RequestPipeline.Tests.Editor
{
    public sealed class RequestPipelineSettingsProviderTests
    {
        [Test]
        public void Create_ReturnsExpectedSettingsPath()
        {
            var provider = RequestPipelineSettingsProvider.Create();

            Assert.That(provider.settingsPath, Is.EqualTo("Project/CodexSix/Request Pipeline"));
        }
    }
}
