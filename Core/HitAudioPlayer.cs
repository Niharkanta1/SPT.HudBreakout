using System;
using System.Collections;
using System.IO;
using EFT;
using UnityEngine;

namespace HudBreakout
{
    internal sealed class HitAudioPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private Transform _sourceListener;
        private AudioClip _hitClip;
        private AudioClip _headshotClip;
        private AudioClip _killClip;
        private readonly System.Random _random = new System.Random();
        private string _audioDirectory;
        private Coroutine _loadCoroutine;
        private int _loadGeneration;
        private bool _listenerWarningLogged;
        private bool _playbackFailureLogged;

        internal void Initialize(string audioDirectory)
        {
            _audioDirectory = audioDirectory;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            ReloadClips();
        }

        internal void ReloadClips()
        {
            _loadGeneration++;
            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }

            if (_source != null) _source.Stop();
            DestroyClip(ref _hitClip);
            DestroyClip(ref _headshotClip);
            DestroyClip(ref _killClip);
            _loadCoroutine = StartCoroutine(LoadClipsWhenAudioIsReady(_loadGeneration));
        }

        private void OnAudioConfigurationChanged(bool _)
        {
            ReloadClips();
        }

        private IEnumerator LoadClipsWhenAudioIsReady(int generation)
        {
            while (AudioSettings.outputSampleRate <= 0 || FindActiveListener() == null)
                yield return null;

            yield return new WaitForSecondsRealtime(1f);

            if (generation != _loadGeneration) yield break;

            _hitClip = LoadClip(Path.Combine(_audioDirectory, "bullet-hit.wav"));
            _headshotClip = LoadClip(Path.Combine(_audioDirectory, "bullet-headshot.wav"));
            _killClip = LoadClip(Path.Combine(_audioDirectory, "bullet-kill.wav"));
            _loadCoroutine = null;
        }

        private static void DestroyClip(ref AudioClip clip)
        {
            if (clip == null) return;
            Destroy(clip);
            clip = null;
        }

        internal void PlayPreview()
        {
            if (!Settings.PlayHitSounds.Value) return;

            var clip = PickRandomLoadedClip();
            if (clip == null)
            {
                HudBreakoutPlugin.Log.LogError("Hit audio preview could not play: no clips loaded.");
                return;
            }
            PlayClip(clip);
        }

        internal void OnHit(HitEvent hitEvent)
        {
            if (!Settings.PlayHitSounds.Value) return;

            var clip = hitEvent.Killed
                ? _killClip
                : hitEvent.Part == EBodyPart.Head ? _headshotClip : _hitClip;

            if (clip == null) return;

            PlayClip(clip);
        }

        private AudioClip PickRandomLoadedClip()
        {
            var availableClips = new AudioClip[3];
            var count = 0;
            if (_hitClip != null) availableClips[count++] = _hitClip;
            if (_headshotClip != null) availableClips[count++] = _headshotClip;
            if (_killClip != null) availableClips[count++] = _killClip;
            return count > 0 ? availableClips[_random.Next(count)] : null;
        }

        private void PlayClip(AudioClip clip)
        {
            var listener = FindActiveListener();
            if (listener == null)
            {
                if (!_listenerWarningLogged)
                {
                    _listenerWarningLogged = true;
                    HudBreakoutPlugin.Log.LogError("Cannot play hit audio because there is no active AudioListener.");
                }
                return;
            }

            if (_source == null || _sourceListener != listener.transform)
            {
                if (_source != null)
                {
                    _source.Stop();
                    Destroy(_source.gameObject);
                }

                var sourceObject = new GameObject("HudBreakoutAudio");
                sourceObject.transform.SetParent(listener.transform, false);
                _source = sourceObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                _source.dopplerLevel = 0f;
                _source.volume = 1f;
                _source.mute = false;
                _source.priority = 0;
                _source.ignoreListenerPause = true;
                _sourceListener = listener.transform;
            }

            if (!_source.isActiveAndEnabled)
            {
                HudBreakoutPlugin.Log.LogError("Cannot play hit audio because its AudioSource is disabled.");
                return;
            }

            try
            {
                _source.PlayOneShot(clip, Settings.HitSoundVolume.Value);
            }
            catch (Exception ex)
            {
                if (!_playbackFailureLogged)
                {
                    _playbackFailureLogged = true;
                    HudBreakoutPlugin.Log.LogError("Failed to play hit audio '" + clip.name + "': " + ex);
                }
            }
        }

        private AudioListener FindActiveListener()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var cameraListener = mainCamera.GetComponent<AudioListener>();
                if (cameraListener != null && cameraListener.isActiveAndEnabled) return cameraListener;
            }

            var listeners = FindObjectsOfType<AudioListener>();
            foreach (var listener in listeners)
                if (listener.isActiveAndEnabled && listener.CompareTag("MainCamera")) return listener;

            if (_sourceListener != null)
            {
                var currentListener = _sourceListener.GetComponent<AudioListener>();
                if (currentListener != null && currentListener.isActiveAndEnabled) return currentListener;
            }

            foreach (var listener in listeners)
                if (listener.isActiveAndEnabled) return listener;

            return null;
        }

        private AudioClip LoadClip(string path)
        {
            if (!File.Exists(path))
            {
                HudBreakoutPlugin.Log.LogError("Hit audio file not found: " + path);
                return null;
            }

            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (ReadFourCc(reader) != "RIFF")
                        throw new InvalidDataException("Missing RIFF header.");

                    reader.ReadUInt32();
                    if (ReadFourCc(reader) != "WAVE")
                        throw new InvalidDataException("Missing WAVE header.");

                    ushort format = 0;
                    ushort channels = 0;
                    uint sampleRate = 0;
                    ushort bitsPerSample = 0;
                    byte[] audioData = null;

                    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                    {
                        var chunkId = ReadFourCc(reader);
                        var chunkSize = reader.ReadUInt32();
                        var chunkEnd = reader.BaseStream.Position + chunkSize;
                        if (chunkEnd > reader.BaseStream.Length)
                            throw new InvalidDataException("WAV chunk extends beyond the end of the file.");

                        if (chunkId == "fmt ")
                        {
                            if (chunkSize < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                            format = reader.ReadUInt16();
                            channels = reader.ReadUInt16();
                            sampleRate = reader.ReadUInt32();
                            reader.ReadUInt32();
                            reader.ReadUInt16();
                            bitsPerSample = reader.ReadUInt16();
                        }
                        else if (chunkId == "data")
                        {
                            audioData = reader.ReadBytes(checked((int)chunkSize));
                        }

                        reader.BaseStream.Position = chunkEnd + (chunkSize & 1);
                    }

                    if (format != 1) throw new InvalidDataException("Only uncompressed PCM WAV audio is supported.");
                    if (channels == 0 || sampleRate == 0) throw new InvalidDataException("WAV has invalid channel or sample-rate metadata.");
                    if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                        throw new InvalidDataException("Unsupported PCM bit depth: " + bitsPerSample);
                    if (audioData == null) throw new InvalidDataException("WAV contains no audio data.");

                    var bytesPerSample = bitsPerSample / 8;
                    var sampleCount = audioData.Length / bytesPerSample;
                    if (sampleCount == 0 || sampleCount % channels != 0)
                        throw new InvalidDataException("WAV audio data has an incomplete sample frame.");

                    var samples = new float[sampleCount];
                    for (var index = 0; index < sampleCount; index++)
                        samples[index] = DecodeSample(audioData, index * bytesPerSample, bitsPerSample);

                    var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path),
                        sampleCount / channels, channels, (int)sampleRate, false);
                    if (clip == null || clip.samples != sampleCount / channels)
                    {
                        var createdSamples = clip == null ? 0 : clip.samples;
                        if (clip != null) Destroy(clip);
                        throw new InvalidOperationException("Unity created an empty audio clip (samples=" +
                            createdSamples + ").");
                    }

                    if (!clip.SetData(samples, 0))
                    {
                        Destroy(clip);
                        throw new InvalidDataException("Unity rejected the decoded audio samples.");
                    }

                    return clip;
                }
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("Failed to load hit audio '" + path + "': " + ex.Message);
                return null;
            }
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            return new string(reader.ReadChars(4));
        }

        private static float DecodeSample(byte[] data, int offset, ushort bitsPerSample)
        {
            switch (bitsPerSample)
            {
                case 8:
                    return (data[offset] - 128) / 128f;
                case 16:
                    return BitConverter.ToInt16(data, offset) / 32768f;
                case 24:
                    var value24 = data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
                    if ((value24 & 0x800000) != 0) value24 |= unchecked((int)0xFF000000);
                    return value24 / 8388608f;
                case 32:
                    return BitConverter.ToInt32(data, offset) / 2147483648f;
                default:
                    throw new InvalidDataException("Unsupported PCM bit depth: " + bitsPerSample);
            }
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            if (_loadCoroutine != null) StopCoroutine(_loadCoroutine);
            if (_source != null)
            {
                _source.Stop();
                Destroy(_source.gameObject);
            }
            DestroyClip(ref _hitClip);
            DestroyClip(ref _headshotClip);
            DestroyClip(ref _killClip);
        }
    }
}
