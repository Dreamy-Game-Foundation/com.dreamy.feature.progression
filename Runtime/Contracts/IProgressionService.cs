using System;

namespace Dreamy.Progression
{
    public interface IProgressionService
    {
        event Action<ProgressionState> ProgressChanged;

        ProgressionState GetState();
        ProgressionAdvanceResult AdvanceNext(string grantId);
        ProgressionAdvanceResult GrantExperience(string grantId, long amount);
    }
}
