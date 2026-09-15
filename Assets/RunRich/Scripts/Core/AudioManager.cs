using UnityEngine;

namespace RunRich
{
    // Играет звуковые эффекты из референсного ассет-пака.
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Clips")]
        [SerializeField] private AudioClip coinClip;
        [SerializeField] private AudioClip removeMoneyClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip victoryClip;
        [SerializeField] private AudioClip jackpotClip;
        [SerializeField] private AudioClip multiplierClip;
        [SerializeField] private AudioClip keyClip;
        [SerializeField] private AudioClip loseClip;
        [SerializeField] private AudioClip boostClip;
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];
        [SerializeField] private AudioClip[] heelClips = new AudioClip[0];

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.75f;
        [SerializeField] private bool useHighHeels;

        public bool Muted { get; private set; }

        private int _footstepIndex;

        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (sfxSource != null) sfxSource.mute = muted;
            if (musicSource != null) musicSource.mute = muted;
        }

        public void ToggleMuted() => SetMuted(!Muted);

        public void PlayCoin() => Play(coinClip, 0.55f, 0.96f, 1.08f);
        public void PlayRemoveMoney() => Play(removeMoneyClip, 0.7f, 0.95f, 1.05f);
        public void PlayClick() => Play(clickClip, 0.6f, 0.98f, 1.02f);
        public void PlayWin() => Play(jackpotClip, 0.7f, 0.98f, 1.02f);
        public void PlayVictory() => Play(victoryClip, 0.8f, 1f, 1f);
        public void PlayLose() => Play(loseClip, 0.7f, 0.98f, 1.02f);
        public void PlayKey() => Play(multiplierClip, 0.65f, 1.02f, 1.06f);
        public void PlayTierUp() => Play(boostClip, 0.6f, 1f, 1f);
        public void PlayMultiplier() => Play(multiplierClip, 0.8f, 1f, 1f);
        public void PlayLevelStart() => Play(clickClip, 0.35f, 0.9f, 0.95f);

        // Чередующиеся шаги бегового цикла.
        public void PlayFootstep(bool flip)
        {
            AudioClip[] set = useHighHeels && heelClips.Length > 0 ? heelClips : footstepClips;
            if (set == null || set.Length == 0) return;

            AudioClip clip = set[_footstepIndex % set.Length];
            _footstepIndex++;
            Play(clip, 0.32f, 0.94f, 1.06f);
        }

        private void Play(AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            if (clip == null || sfxSource == null || Muted) return;
            sfxSource.pitch = Random.Range(pitchMin, pitchMax);
            sfxSource.PlayOneShot(clip, volume * sfxVolume);
        }

        // Прокидывание ссылок в редакторе, использует сборщик уровней.
        public void EditorAssign(AudioSource sfx, AudioSource music, AudioClip coin, AudioClip removeMoney, AudioClip click,
            AudioClip victory, AudioClip jackpot, AudioClip multiplier, AudioClip key, AudioClip lose, AudioClip boost,
            AudioClip[] footsteps, AudioClip[] heels)
        {
            sfxSource = sfx;
            musicSource = music;
            coinClip = coin;
            removeMoneyClip = removeMoney;
            clickClip = click;
            victoryClip = victory;
            jackpotClip = jackpot;
            multiplierClip = multiplier;
            keyClip = key;
            loseClip = lose;
            boostClip = boost;
            footstepClips = footsteps;
            heelClips = heels;
        }

        public void EditorSetHighHeels(bool value) => useHighHeels = value;
    }
}
