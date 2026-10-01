using Dreamy.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Feature.Progression.Integration
{
    public sealed class ProgressionHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject experienceRoot;
        [SerializeField] private Image experienceFillImage;
        [SerializeField] private TMP_Text experienceText;

        private IProgressionService progression;

        public void Bind(IProgressionService progressionService)
        {
            if (ReferenceEquals(progression, progressionService)) return;
            Unbind();
            progression = progressionService;
            if (progression == null)
            {
                Clear();
                return;
            }

            progression.ProgressChanged += Render;
            Render(progression.GetState());
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (progression == null) return;
            progression.ProgressChanged -= Render;
            progression = null;
        }

        private void Render(ProgressionState state)
        {
            if (levelText != null) levelText.text = $"Level {state.CurrentLevel}/{state.MaxLevel}";
            bool hasExperience = state.Mode == ProgressionMode.Experience;
            if (experienceRoot != null) experienceRoot.SetActive(hasExperience);
            if (!hasExperience) return;
            if (experienceFillImage != null)
            {
                experienceFillImage.fillAmount = state.ExperienceToNextLevel > 0
                    ? Mathf.Clamp01((float)state.ExperienceIntoCurrentLevel / state.ExperienceToNextLevel)
                    : 1f;
            }

            if (experienceText != null)
            {
                experienceText.text = state.ExperienceToNextLevel > 0
                    ? $"{state.ExperienceIntoCurrentLevel}/{state.ExperienceToNextLevel} XP"
                    : $"{state.TotalExperience} XP";
            }
        }

        private void Clear()
        {
            if (levelText != null) levelText.text = string.Empty;
            if (experienceRoot != null) experienceRoot.SetActive(false);
        }
    }
}
