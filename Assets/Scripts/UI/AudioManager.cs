using System.Collections.Generic;
using UnityEngine;

/// HOW TO USE:
///   AudioManager.Instance.Play("GunShot");
///   AudioManager.Instance.PlayAt("GunShot", transform.position);
///   AudioManager.Instance.PlayMusic("TitleTheme");
///   AudioManager.Instance.StopMusic();
/// 
/// HOW TO SET UP:
///   1. Create an empty GameObject in your scene called "AudioManager".
///   2. Add this script to it.
///   3. In the Inspector, add entries to the "sounds" list.
///      Each entry has: a Name (what you call it in code), an AudioClip, Volume, and Pitch.
///   4. Wire up a "Music Source" AudioSource for background music (or leave blank to auto-create).

public class AudioManager : MonoBehaviour
{
    // ===================== Singleton =====================

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookup();
        SetupMusicSource();
    }

    // ===================== Data Classes =====================

    [System.Serializable]
    public class Sound
    {
        [Tooltip("The name used in code: AudioManager.Instance.Play(\"GunShot\")")]
        public string name;

        [Tooltip("Drag the AudioClip here.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.1f, 3f)]
        public float pitch = 1f;

        [Tooltip("If true, this sound loops. Good for ambience.")]
        public bool loop = false;
    }

    // ===================== Inspector Fields =====================

    [Header("Sound Library")]
    [Tooltip("Add all your game sounds here. Name them to match what you call in code.")]
    [SerializeField] private List<Sound> sounds = new List<Sound>();

    [Header("Music")]
    [Tooltip("Drag an AudioSource here, or one will be created automatically.")]
    [SerializeField] private AudioSource musicSource;

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.6f;

    // ===================== Internal =====================

    private Dictionary<string, Sound> soundLookup;

    // ===================== Setup =====================

    private void BuildLookup()
    {
        // Dictionary lets us find a Sound by name instantly instead of looping every time.
        soundLookup = new Dictionary<string, Sound>();
        foreach (Sound s in sounds)
        {
            if (string.IsNullOrEmpty(s.name))
            {
                Debug.LogWarning("[AudioManager] A sound entry has no name. It will be skipped.");
                continue;
            }

            if (soundLookup.ContainsKey(s.name))
            {
                Debug.LogWarning("[AudioManager] Duplicate sound name '" + s.name + "'. The second one will be ignored.");
                continue;
            }

            soundLookup[s.name] = s;
        }

        Debug.Log("[AudioManager] Loaded " + soundLookup.Count + " sounds.");
    }

    private void SetupMusicSource()
    {
        // If the music source wasn't manually wired, create one on this object.
        if (!musicSource)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        musicSource.volume = musicVolume * masterVolume;
    }

    // ===================== Public API: SFX =====================

    /// <summary>
    /// Plays a sound at the camera/listener position (2D, no falloff).
    /// Use this for UI sounds, gun fire, reload clicks, etc.
    /// </summary>
    public void Play(string soundName)
    {
        Sound s = GetSound(soundName);
        if (s == null) return;

        // AudioSource.PlayClipAtPoint is convenient but doesn't support pitch.
        // We create a temporary GameObject instead so we can set pitch too.
        PlaySoundInternal(s, Vector3.zero, spatialize: false);
    }

    /// <summary>
    /// Plays a sound at a world-space position (3D, falls off with distance).
    /// Use this for sounds that should come from a point in the world.
    /// </summary>
    public void PlayAt(string soundName, Vector3 worldPosition)
    {
        Sound s = GetSound(soundName);
        if (s == null) return;

        PlaySoundInternal(s, worldPosition, spatialize: true);
    }

    // ===================== Public API: Music =====================

    /// <summary>
    /// Plays background music. Stops any currently playing music first.
    /// </summary>
    public void PlayMusic(string soundName)
    {
        Sound s = GetSound(soundName);
        if (s == null) return;

        musicSource.Stop();
        musicSource.clip = s.clip;
        musicSource.volume = s.volume * musicVolume * masterVolume;
        musicSource.pitch = s.pitch;
        musicSource.loop = true;
        musicSource.Play();
    }

    /// <summary>
    /// Stops any playing background music.
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
    }

    /// <summary>
    /// Pauses background music without losing position.
    /// </summary>
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    /// <summary>
    /// Resumes paused music.
    /// </summary>
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    // ===================== Public API: Volume =====================

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume * masterVolume;
    }

    public void SetSfxVolume(float value) => sfxVolume = Mathf.Clamp01(value);

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume * masterVolume;
    }

    // ===================== Internal Helpers =====================

    private Sound GetSound(string soundName)
    {
        if (soundLookup == null)
        {
            Debug.LogError("[AudioManager] Sound lookup not built yet. Was Awake called?");
            return null;
        }

        if (!soundLookup.TryGetValue(soundName, out Sound s))
        {
            Debug.LogWarning("[AudioManager] Sound '" + soundName + "' not found. Check the name in the Inspector.");
            return null;
        }

        if (s.clip == null)
        {
            Debug.LogWarning("[AudioManager] Sound '" + soundName + "' has no AudioClip assigned.");
            return null;
        }

        return s;
    }

    private void PlaySoundInternal(Sound s, Vector3 position, bool spatialize)
    {
        // Spawn a temp object to play the sound (so we can control pitch).
        GameObject tempObj = new GameObject("AudioOneShot_" + s.name);

        if (spatialize)
            tempObj.transform.position = position;

        AudioSource source = tempObj.AddComponent<AudioSource>();
        source.clip = s.clip;
        source.volume = s.volume * sfxVolume * masterVolume;
        source.pitch = s.pitch;
        source.loop = s.loop;
        source.spatialBlend = spatialize ? 1f : 0f; // 1 = full 3D, 0 = 2D
        source.Play();

        // Clean up the temp object after the clip finishes.
        if (!s.loop)
            Destroy(tempObj, s.clip.length / s.pitch + 0.1f);
    }
}