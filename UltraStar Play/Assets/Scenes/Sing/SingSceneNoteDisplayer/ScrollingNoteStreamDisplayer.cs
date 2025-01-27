﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniInject;
using UniRx;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class ScrollingNoteStreamDisplayer : AbstractSingSceneNoteDisplayer
{
    [InjectedInInspector]
    public RectTransform lyricsBar;

    public float pitchIndicatorAnchorX = 0.15f;
    public float displayedNoteDurationInSeconds = 5;

    [Inject]
    private SongAudioPlayer songAudioPlayer;

    [Inject]
    private Voice voice;

    private List<Note> upcomingNotes = new List<Note>();

    private int micDelayInMillis;
    private int displayedBeats;

    private int frameCount;

    private void Update()
    {
        // For some reason, Unity seems to need some frames to finish the calculation of the lyricsBar position.
        // In the first frame, the lyrics positions are wrong. Thus, as a workaround, delay the Update code by one frame.
        if (frameCount > 2)
        {
            RemoveNotesOutsideOfDisplayArea();
            CreateNotesInDisplayArea();

            UpdateUiNotePositions();
        }
        else
        {
            frameCount++;
        }
    }

    override public void Init(int lineCount)
    {
        if (micProfile != null)
        {
            micDelayInMillis = micProfile.DelayInMillis;
        }
        base.Init(lineCount);

        upcomingNotes = voice.Sentences
            .SelectMany(sentence => sentence.Notes)
            .ToList();
        upcomingNotes.Sort(Note.comparerByStartBeat);

        avgMidiNote = CalculateAvgMidiNote(voice.Sentences.SelectMany(sentence => sentence.Notes).ToList());
        maxNoteRowMidiNote = avgMidiNote + (noteRowCount / 2);
        minNoteRowMidiNote = avgMidiNote - (noteRowCount / 2);

        displayedBeats = (int)Math.Ceiling(BpmUtils.GetBeatsPerSecond(songMeta) * displayedNoteDurationInSeconds);
    }

    override protected void PositionUiNote(RectTransform uiNote, int midiNote, double noteStartBeat, double noteEndBeat)
    {
        // The VerticalPitchIndicator's position is the position where recording happens.
        // Thus, a note with startBeat == (currentBeat + micDelayInBeats) will have its left side drawn where the VerticalPitchIndicator is.
        double millisInSong = songAudioPlayer.PositionInSongInMillis - micDelayInMillis;
        double currentBeatConsideringMicDelay = BpmUtils.MillisecondInSongToBeat(songMeta, millisInSong);

        Vector2 anchorY = GetAnchorYForMidiNote(midiNote);
        float anchorXStart = (float)((noteStartBeat - currentBeatConsideringMicDelay) / displayedBeats) + pitchIndicatorAnchorX;
        float anchorXEnd = (float)((noteEndBeat - currentBeatConsideringMicDelay) / displayedBeats) + pitchIndicatorAnchorX;

        uiNote.anchorMin = new Vector2(anchorXStart, anchorY.x);
        uiNote.anchorMax = new Vector2(anchorXEnd, anchorY.y);
        uiNote.MoveCornersToAnchors();
    }

    override protected UiNote CreateUiNote(Note note)
    {
        UiNote uiNote = base.CreateUiNote(note);
        if (uiNote != null)
        {
            // Freestyle notes are not drawn
            if (uiNote.Note.IsFreestyle)
            {
                uiNote.image.enabled = false;
            }

            PositionUiNoteLyrics(uiNote);
        }
        return uiNote;
    }

    private void PositionUiNoteLyrics(UiNote uiNote)
    {
        // Position lyrics. Width until next note, vertically centered on lyricsBar.
        uiNote.lyricsUiText.enabled = true;
        uiNote.lyricsUiText.color = Color.white;
        uiNote.lyricsUiText.alignment = TextAnchor.MiddleLeft;

        RectTransform lyricsRectTransform = uiNote.lyricsUiTextRectTransform;
        lyricsRectTransform.SetParent(uiNote.transform.parent, true);
        PositionUiNote(lyricsRectTransform, 60, uiNote.Note.StartBeat, GetNoteStartBeatOfFollowingNote(uiNote.Note));
        lyricsRectTransform.SetParent(lyricsBar, true);
        lyricsRectTransform.anchorMin = new Vector2(lyricsRectTransform.anchorMin.x, 0);
        lyricsRectTransform.anchorMax = new Vector2(lyricsRectTransform.anchorMax.x, 1);
        lyricsRectTransform.sizeDelta = new Vector2(lyricsRectTransform.sizeDelta.x, 0);
        lyricsRectTransform.localPosition = new Vector2(lyricsRectTransform.localPosition.x, 0);
        uiNote.lyricsUiText.transform.SetParent(uiNote.RectTransform, true);
    }

    private double GetNoteStartBeatOfFollowingNote(Note note)
    {
        Sentence sentence = note.Sentence;
        if (sentence == null)
        {
            return note.EndBeat;
        }

        Note followingNote = sentence.Notes
            .Where(otherNote => otherNote.StartBeat >= note.EndBeat)
            .OrderBy(otherNote => otherNote.StartBeat)
            .FirstOrDefault();
        if (followingNote != null)
        {
            if (note.EndBeat == followingNote.StartBeat)
            {
                return note.EndBeat;
            }
            else
            {
                // Add a little bit spacing
                return followingNote.StartBeat - 1;
            }
        }
        else
        {
            return sentence.ExtendedMaxBeat;
        }
    }

    private void UpdateUiNotePositions()
    {
        foreach (UiNote uiNote in noteToUiNoteMap.Values)
        {
            PositionUiNote(uiNote.RectTransform, uiNote.Note.MidiNote, uiNote.Note.StartBeat, uiNote.Note.EndBeat);
        }

        foreach (UiRecordedNote uiRecordedNote in uiRecordedNotes)
        {
            // Draw the UiRecordedNotes smoothly from their StartBeat to TargetEndBeat
            if (uiRecordedNote.EndBeat < uiRecordedNote.TargetEndBeat)
            {
                UpdateUiRecordedNoteEndBeat(uiRecordedNote);
            }

            PositionUiNote(uiRecordedNote.RectTransform, uiRecordedNote.MidiNote, uiRecordedNote.StartBeat, uiRecordedNote.EndBeat);
        }
    }

    private void CreateNotesInDisplayArea()
    {
        // Create UiNotes to fill the display area
        int displayAreaMinBeat = CalculateDisplayAreaMinBeat();
        int displayAreaMaxBeat = CalculateDisplayAreaMaxBeat();

        List<Note> newNotes = new List<Note>();
        foreach (Note note in upcomingNotes)
        {
            if (displayAreaMinBeat <= note.StartBeat && note.StartBeat <= displayAreaMaxBeat)
            {
                newNotes.Add(note);
            }
            else if (note.StartBeat > displayAreaMaxBeat)
            {
                // The upcoming notes are sorted. Thus, all following notes will not be inside the drawingArea as well.
                break;
            }
        }

        // Create UiNotes
        foreach (Note note in newNotes)
        {
            // The note is not upcoming anymore
            upcomingNotes.Remove(note);
            CreateUiNote(note);
        }
    }

    private void RemoveNotesOutsideOfDisplayArea()
    {
        int displayAreaMinBeat = CalculateDisplayAreaMinBeat();
        foreach (UiNote uiNote in noteToUiNoteMap.Values.ToList())
        {
            if (uiNote.Note.EndBeat < displayAreaMinBeat)
            {
                RemoveUiNote(uiNote);
            }
        }
        foreach (UiRecordedNote uiRecordedNote in uiRecordedNotes.ToList())
        {
            if (uiRecordedNote.EndBeat < displayAreaMinBeat)
            {
                RemoveUiRecordedNote(uiRecordedNote);
            }
        }
    }

    private int CalculateAvgMidiNote(IReadOnlyCollection<Note> notes)
    {
        return notes.Count > 0
            ? (int)notes.Select(it => it.MidiNote).Average()
            : 0;
    }

    private int CalculateDisplayAreaMinBeat()
    {
        // This is an over-approximation of the visible displayArea
        return (int)songAudioPlayer.CurrentBeat - displayedBeats / 2;
    }

    private int CalculateDisplayAreaMaxBeat()
    {
        // This is an over-approximation of the visible displayArea
        return (int)songAudioPlayer.CurrentBeat + displayedBeats;
    }
}
