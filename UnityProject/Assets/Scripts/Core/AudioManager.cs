using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Audio;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class AudioManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private AudioConfig _config;
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _uiSource;
        [SerializeField] private List<AudioSource> _sfxSources = new();
        [SerializeField] private List<AudioSource> _voiceSources = new();

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _clipHandles = new();
        private string _currentMusic;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private float _voiceVolume = 1f;
        private float _ambientVolume = 1f;

        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                _musicSource.volume = _musicVolume;
                _audioMixer?.SetFloat("MusicVolume", Mathf.Log10(_musicVolume) * 20);
            }
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                foreach (var source in _sfxSources)
                {
                    source.volume = _sfxVolume;
                }
                _audioMixer?.SetFloat("SFXVolume", Mathf.Log10(_sfxVolume) * 20);
            }
        }

        public float VoiceVolume
        {
            get => _voiceVolume;
            set
            {
                _voiceVolume = Mathf.Clamp01(value);
                foreach (var source in _voiceSources)
                {
                    source.volume = _voiceVolume;
                }
                _audioMixer?.SetFloat("VoiceVolume", Mathf.Log10(_voiceVolume) * 20);
            }
        }

        public async UniTask InitializeAsync()
        {
            // Load saved volumes
            _musicVolume = PlayerPrefs.GetFloat("volume_music", 1f);
            _sfxVolume = PlayerPrefs.GetFloat("volume_sfx", 1f);
            _voiceVolume = PlayerPrefs.GetFloat("volume_voice", 1f);
            _ambientVolume = PlayerPrefs.GetFloat("volume_ambient", 1f);

            MusicVolume = _musicVolume;
            SFXVolume = _sfxVolume;
            VoiceVolume = _voiceVolume;

            // Ensure we have enough audio sources
            EnsureAudioSources();
        }

        public void Shutdown()
        {
            StopAllAudio();

            foreach (var handle in _clipHandles.Values)
            {
                Addressables.Release(handle);
            }
            _clipHandles.Clear();
            _clipCache.Clear();
        }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public async UniTask PlayMusicAsync(string musicName, float fadeTime = 1f, bool loop = true)
        {
            if (_currentMusic == musicName && _musicSource.isPlaying) return;

            var clip = await LoadClipAsync(musicName);
            if (clip == null) return;

            if (_musicSource.isPlaying)
            {
                await FadeOutAsync(_musicSource, fadeTime);
            }

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = 0f;
            _musicSource.Play();

            await FadeInAsync(_musicSource, _musicVolume, fadeTime);

            _currentMusic = musicName;
        }

        public async UniTask StopMusicAsync(float fadeTime = 1f)
        {
            if (!_musicSource.isPlaying) return;

            await FadeOutAsync(_musicSource, fadeTime);
            _musicSource.Stop();
            _musicSource.clip = null;
            _currentMusic = null;
        }

        public void PlaySFX(string sfxName, float volume = 1f, float pitch = 1f)
        {
            var clip = LoadClip(sfxName);
            if (clip == null) return;

            var source = GetAvailableSFXSource();
            if (source == null) return;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume * _sfxVolume);
        }

        public void PlaySFXAtPosition(string sfxName, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var clip = LoadClip(sfxName);
            if (clip == null) return;

            AudioSource.PlayClipAtPoint(clip, position, volume * _sfxVolume);
        }

        public void PlayVoice(string voiceName, float volume = 1f)
        {
            var clip = LoadClip(voiceName);
            if (clip == null) return;

            var source = GetAvailableVoiceSource();
            if (source == null) return;

            source.PlayOneShot(clip, volume * _voiceVolume);
        }

        public void PlayUI(string uiName, float volume = 1f)
        {
            var clip = LoadClip(uiName);
            if (clip == null) return;

            _uiSource.PlayOneShot(clip, volume * _sfxVolume);
        }

        public void PlayAmbient(string ambientName, float volume = 1f, bool loop = true)
        {
            var clip = LoadClip(ambientName);
            if (clip == null) return;

            _ambientSource.clip = clip;
            _ambientSource.loop = loop;
            _ambientSource.volume = volume * _ambientVolume;
            _ambientSource.Play();
        }

        public void StopAmbient()
        {
            _ambientSource.Stop();
            _ambientSource.clip = null;
        }

        public void SetMasterVolume(float volume)
        {
            _audioMixer?.SetFloat("MasterVolume", Mathf.Log10(Mathf.Clamp01(volume)) * 20);
        }

        public void PauseAll()
        {
            _musicSource.Pause();
            _ambientSource.Pause();
            _uiSource.Pause();
            foreach (var source in _sfxSources) source.Pause();
            foreach (var source in _voiceSources) source.Pause();
        }

        public void ResumeAll()
        {
            _musicSource.UnPause();
            _ambientSource.UnPause();
            _uiSource.UnPause();
            foreach (var source in _sfxSources) source.UnPause();
            foreach (var source in _voiceSources) source.UnPause();
        }

        public void StopAllAudio()
        {
            _musicSource.Stop();
            _ambientSource.Stop();
            _uiSource.Stop();
            foreach (var source in _sfxSources) source.Stop();
            foreach (var source in _voiceSources) source.Stop();
        }

        private AudioSource GetAvailableSFXSource()
        {
            foreach (var source in _sfxSources)
            {
                if (!source.isPlaying)
                    return source;
            }

            // Return the one that will finish soonest
            return _sfxSources[0];
        }

        private AudioSource GetAvailableVoiceSource()
        {
            foreach (var source in _voiceSources)
            {
                if (!source.isPlaying)
                    return source;
            }

            return _voiceSources[0];
        }

        private void EnsureAudioSources()
        {
            // Ensure we have at least 8 SFX sources
            while (_sfxSources.Count < 8)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.volume = _sfxVolume;
                _sfxSources.Add(source);
            }

            // Ensure we have at least 4 voice sources
            while (_voiceSources.Count < 4)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.volume = _voiceVolume;
                _voiceSources.Add(source);
            }
        }

        private AudioClip LoadClip(string name)
        {
            if (_clipCache.TryGetValue(name, out var clip))
            {
                return clip;
            }

            // Try to load synchronously from Resources
            clip = Resources.Load<AudioClip>($"Audio/{name}");
            if (clip != null)
            {
                _clipCache[name] = clip;
            }

            return clip;
        }

        private async UniTask<AudioClip> LoadClipAsync(string name)
        {
            if (_clipCache.TryGetValue(name, out var clip))
            {
                return clip;
            }

            // Load from Addressables
            var handle = Addressables.LoadAssetAsync<AudioClip>($"audio/{name}");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                clip = handle.Result;
                _clipCache[name] = clip;
                _clipHandles[name] = handle;
                return clip;
            }

            // Fallback to Resources
            clip = Resources.Load<AudioClip>($"Audio/{name}");
            if (clip != null)
            {
                _clipCache[name] = clip;
            }

            return clip;
        }

        private async UniTask FadeInAsync(AudioSource source, float targetVolume, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                await UniTask.Yield();
            }
            source.volume = targetVolume;
        }

        private async UniTask FadeOutAsync(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                await UniTask.Yield();
            }
            source.volume = 0f;
        }

        public void SaveVolumes()
        {
            PlayerPrefs.SetFloat("volume_music", _musicVolume);
            PlayerPrefs.SetFloat("volume_sfx", _sfxVolume);
            PlayerPrefs.SetFloat("volume_voice", _voiceVolume);
            PlayerPrefs.SetFloat("volume_ambient", _ambientVolume);
            PlayerPrefs.Save();
        }
    }

    [System.Serializable]
    public class AudioConfig
    {
        public int MaxSFXSources = 16;
        public int MaxVoiceSources = 8;
        public float DefaultMusicFadeTime = 1f;
        public bool Enable3DAudio = false;
    }
}