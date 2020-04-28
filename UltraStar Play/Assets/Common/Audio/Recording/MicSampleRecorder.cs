﻿using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicSampleRecorder : MonoBehaviour
{
    private const int DefaultSampleRateHz = 44100;

    public bool playRecordedAudio;

    private int micAmplifyMultiplier;
    private MicProfile micProfile;
    public MicProfile MicProfile
    {
        get
        {
            return micProfile;
        }
        set
        {
            bool restartPitchDetection = IsRecording;
            if (IsRecording)
            {
                StopRecording();
            }
            micProfile = value;
            if (micProfile != null && !micProfile.Name.IsNullOrEmpty())
            {
                Microphone.GetDeviceCaps(micProfile.Name, out int minFrequency, out int maxFrequency);
                Debug.Log($"Mic frequency range: {minFrequency} - {maxFrequency} Hz");
                SampleRateHz = maxFrequency;
                MicSamples = new float[SampleRateHz];
                if (restartPitchDetection)
                {
                    StartRecording();
                }
            }
        }
    }

    public float[] MicSamples { get; private set; } = new float[DefaultSampleRateHz];
    public int SampleRateHz { get; private set; } = DefaultSampleRateHz;

    private Subject<RecordingEvent> recordingEventStream = new Subject<RecordingEvent>();
    public IObservable<RecordingEvent> RecordingEventStream
    {
        get
        {
            return recordingEventStream;
        }
    }

    private AudioSource audioSource;
    private AudioClip micAudioClip;

    public bool IsRecording { get; private set; }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnDisable()
    {
        StopRecording();
    }

    void Update()
    {
        UpdateMicrophoneAudioPlayback();
        UpdateRecording();
    }

    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }
        IsRecording = true;

        // Check for microphone existence.
        List<string> soundcards = new List<string>(UnityEngine.Microphone.devices);
        if (!soundcards.Contains(MicProfile.Name))
        {
            string micDevicesCsv = string.Join(",", soundcards);
            Debug.LogError($"Did not find mic '{MicProfile.Name}'. Available mic devices: {micDevicesCsv}");
            IsRecording = false;
            return;
        }
        Debug.Log($"Starting recording with '{MicProfile.Name}'");

        micAmplifyMultiplier = micProfile.AmplificationMultiplier();

        // Code for low-latency microphone input taken from
        // https://support.unity3d.com/hc/en-us/articles/206485253-How-do-I-get-Unity-to-playback-a-Microphone-input-in-real-time-
        micAudioClip = UnityEngine.Microphone.Start(MicProfile.Name, true, 1, SampleRateHz);
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        while (UnityEngine.Microphone.GetPosition(MicProfile.Name) <= 0)
        {
            // <Busy waiting>
            // Emergency exit
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                IsRecording = false;
                return;
            }
        }

        // Configure audio playback
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = micAudioClip;
        audioSource.loop = true;
    }

    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        Debug.Log($"Stopping recording with '{MicProfile.Name}'");
        UnityEngine.Microphone.End(MicProfile.Name);
        IsRecording = false;
    }

    private void UpdateRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        if (micAudioClip == null)
        {
            Debug.LogError("AudioClip for microphone is null");
            StopRecording();
            return;
        }

        // Fill buffer with raw sample data from microphone
        int currentSamplePosition = UnityEngine.Microphone.GetPosition(MicProfile.Name);
        micAudioClip.GetData(MicSamples, currentSamplePosition);

        // Process the portion that has been buffered by Unity since the last frame.
        // New samples come into the buffer "from the right", i.e., highest index holds the newest sample.
        int samplesSinceLastFrame = (int)(SampleRateHz * Time.deltaTime);
        int newSamplesStartIndex = MicSamples.Length - samplesSinceLastFrame;
        int newSamplesEndIndex = MicSamples.Length - 1;
        ApplyMicAmplification(MicSamples, newSamplesStartIndex, newSamplesEndIndex);

        // Notify listeners
        RecordingEvent recordingEvent = new RecordingEvent(MicSamples, newSamplesStartIndex, newSamplesEndIndex);
        recordingEventStream.OnNext(recordingEvent);
    }

    private void ApplyMicAmplification(float[] buffer, int startIndex, int endIndex)
    {
        if (micAmplifyMultiplier == 0)
        {
            return;
        }
        float newSample;
        for (int index = startIndex; index < endIndex; index++)
        {
            newSample = buffer[index] * micAmplifyMultiplier;
            if (newSample > 1)
            {
                newSample = 1;
            }
            else if (newSample < -1)
            {
                newSample = -1;
            }
            buffer[index] = newSample;
        }
    }

    private void UpdateMicrophoneAudioPlayback()
    {
        if (playRecordedAudio && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (!playRecordedAudio && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    public class RecordingEvent
    {
        public float[] MicSamples { get; private set; }
        public int NewSamplesStartIndex { get; private set; }
        public int NewSamplesEndIndex { get; private set; }

        public RecordingEvent(float[] micBuffer, int newSamplesStartIndex, int newSamplesEndIndex)
        {
            MicSamples = micBuffer;
            NewSamplesStartIndex = newSamplesStartIndex;
            NewSamplesEndIndex = newSamplesEndIndex;
        }
    }
}
