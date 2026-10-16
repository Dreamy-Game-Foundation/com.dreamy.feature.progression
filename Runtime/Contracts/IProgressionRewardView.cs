using System.Collections.Generic;

namespace Dreamy.Progression
{
    public interface IProgressionRewardView
    {
        void Render(IReadOnlyList<ProgressionRewardState> rewards);
    }
}
