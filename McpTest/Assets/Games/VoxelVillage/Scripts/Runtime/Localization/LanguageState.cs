#nullable enable

using System;

namespace McpTest.VoxelVillage
{
    public sealed class LanguageState
    {
        LanguageCode _current;

        public LanguageState(LanguageCode initialLanguage = LanguageCode.Ko)
        {
            _current = initialLanguage;
        }

        public event Action<LanguageCode>? Changed;

        public LanguageCode Current => _current;

        public void CycleNext()
        {
            Set(_current.Next());
        }

        public void Set(LanguageCode language)
        {
            if (_current == language)
            {
                return;
            }

            _current = language;
            Changed?.Invoke(_current);
        }
    }
}
