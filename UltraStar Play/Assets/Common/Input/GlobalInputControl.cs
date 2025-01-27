﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniInject;
using UniRx;
 
public class GlobalInputControl : MonoBehaviour, INeedInjection
{
    [Inject]
    private Settings settings;
    
    private void Start()
    {
        // Toggle full-screen mode via F11
        InputManager.GetInputAction(R.InputActions.usplay_toggleFullscreen).PerformedAsObservable()
            .Subscribe(_ => ToggleFullscreen());

        // Mute / unmute audio via F12
        InputManager.GetInputAction(R.InputActions.usplay_toggleMute).PerformedAsObservable()
            .Subscribe(_ => AudioManager.ToggleMuteAudio());
    }

    private void ToggleFullscreen()
    {
        Debug.Log("Toggle full-screen mode");
        Screen.fullScreen = !Screen.fullScreen;
        // A full-screen switch does not happen immediately; it will actually happen when the current frame is finished.
        StartCoroutine(CoroutineUtils.ExecuteAfterDelayInFrames(2,
            () =>
            {
                settings.GraphicSettings.fullScreenMode = Screen.fullScreenMode;
                Debug.Log("New full-screen mode " + settings.GraphicSettings.fullScreenMode);
            }));
    }
}
