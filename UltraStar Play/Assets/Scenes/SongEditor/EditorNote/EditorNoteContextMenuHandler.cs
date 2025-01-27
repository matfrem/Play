﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniInject;
using UniRx;
using System.Text;
using UnityEngine.InputSystem;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class EditorNoteContextMenuHandler : AbstractContextMenuHandler
{
    [Inject]
    private SongMeta songMeta;

    [Inject]
    private SongEditorSelectionController selectionController;

    [Inject]
    private DeleteNotesAction deleteNotesAction;

    [Inject]
    private SplitNotesAction splitNotesAction;

    [Inject]
    private MergeNotesAction mergeNotesAction;

    [Inject]
    private MergeSentencesAction mergeSentencesAction;

    [Inject]
    private SetNoteTypeAction setNoteTypeAction;

    [Inject]
    private MoveNoteToAjacentSentenceAction moveNoteToAdjacentSentenceAction;

    [Inject]
    private MoveNotesToOtherVoiceAction moveNotesToOtherVoiceAction;

    [Inject]
    private SpaceBetweenNotesAction spaceBetweenNotesAction;

    [Inject]
    private NoteAreaContextMenuHandler noteAreaContextMenuHandler;
    
    [Inject]
    private NoteAreaDragHandler noteAreaDragHandler;
    
    [Inject]
    private SongEditorSceneController songEditorSceneController;
    
    private EditorUiNote uiNote;

    protected override void CheckOpenContextMenuFromInputAction(InputAction.CallbackContext context)
    {
        // This ContextMenu could open although a drag is in progress.
        if (noteAreaContextMenuHandler.IsDrag
            || noteAreaDragHandler.DragDistance.magnitude > AbstractContextMenuHandler.DragDistanceThreshold)
        {
            return;
        }
        
        base.CheckOpenContextMenuFromInputAction(context);
    }

    protected override void FillContextMenu(ContextMenu contextMenu)
    {
        if (uiNote == null)
        {
            uiNote = GetComponent<EditorUiNote>();
        }
        if (!selectionController.IsSelected(uiNote.Note))
        {
            selectionController.SetSelection(new List<EditorUiNote> { uiNote });
        }

        List<Note> selectedNotes = selectionController.GetSelectedNotes();

        contextMenu.AddItem("Edit lyrics", () => songEditorSceneController.StartEditingNoteText());
        FillContextMenuToSplitAndMergeNotes(contextMenu, selectedNotes);
        FillContextMenuToAddSpaceBetweenNotes(contextMenu, selectedNotes);
        FillContextMenuToSetNoteType(contextMenu, selectedNotes);
        FillContextMenuToMergeSentences(contextMenu, selectedNotes);
        FillContextMenuToMoveToOtherSentence(contextMenu, selectedNotes);
        FillContextMenuToMoveToOtherVoice(contextMenu, selectedNotes);
        FillContextMenuToDeleteNotes(contextMenu, selectedNotes);
    }

    private void FillContextMenuToAddSpaceBetweenNotes(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        contextMenu.AddSeparator();
        if (spaceBetweenNotesAction.CanExecute(selectedNotes))
        {
            contextMenu.AddItem("Add space between notes", () =>
            {
                SpaceBetweenNotesButton spaceBetweenNotesButton = FindObjectOfType<SpaceBetweenNotesButton>(true);
                if (int.TryParse(spaceBetweenNotesButton.numberOfBeatsInputField.text, out int spaceInBeats))
                {
                    spaceBetweenNotesAction.ExecuteAndNotify(selectedNotes, spaceInBeats);
                }
            });
        }
    }

    private void FillContextMenuToDeleteNotes(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        contextMenu.AddSeparator();
        contextMenu.AddItem("Delete", () => deleteNotesAction.ExecuteAndNotify(selectedNotes));
    }

    private void FillContextMenuToSplitAndMergeNotes(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        if (splitNotesAction.CanExecute(selectedNotes))
        {
            contextMenu.AddItem("Split Notes", () => splitNotesAction.ExecuteAndNotify(selectedNotes));
        }
        if (mergeNotesAction.CanExecute(selectedNotes))
        {
            contextMenu.AddItem("Merge Notes", () => mergeNotesAction.ExecuteAndNotify(selectedNotes, uiNote.Note));
        }
    }

    private void FillContextMenuToSetNoteType(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        contextMenu.AddSeparator();
        if (setNoteTypeAction.CanExecute(selectedNotes, ENoteType.Golden))
        {
            contextMenu.AddItem("Make golden",
                () => setNoteTypeAction.ExecuteAndNotify(selectedNotes, ENoteType.Golden));
        }
        if (setNoteTypeAction.CanExecute(selectedNotes, ENoteType.Freestyle))
        {
            contextMenu.AddItem("Make freestyle",
                () => setNoteTypeAction.ExecuteAndNotify(selectedNotes, ENoteType.Freestyle));
        }
        if (setNoteTypeAction.CanExecute(selectedNotes, ENoteType.Rap))
        {
            contextMenu.AddItem("Make rap",
                () => setNoteTypeAction.ExecuteAndNotify(selectedNotes, ENoteType.Rap));
        }
        if (setNoteTypeAction.CanExecute(selectedNotes, ENoteType.RapGolden))
        {
            contextMenu.AddItem("Make rap-golden",
                () => setNoteTypeAction.ExecuteAndNotify(selectedNotes, ENoteType.RapGolden));
        }
        if (setNoteTypeAction.CanExecute(selectedNotes, ENoteType.Normal))
        {
            contextMenu.AddItem("Make normal",
                () => setNoteTypeAction.ExecuteAndNotify(selectedNotes, ENoteType.Normal));
        }
    }

    private void FillContextMenuToMergeSentences(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        if (mergeSentencesAction.CanExecute(selectedNotes))
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Merge sentences",
                () => mergeSentencesAction.ExecuteAndNotify(selectedNotes, uiNote.Note));
        }
    }

    private void FillContextMenuToMoveToOtherVoice(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        bool canMoveToVoice1 = moveNotesToOtherVoiceAction.CanMoveNotesToVoice(selectedNotes, Voice.soloVoiceName, Voice.firstVoiceName);
        bool canMoveToVoice2 = moveNotesToOtherVoiceAction.CanMoveNotesToVoice(selectedNotes, Voice.secondVoiceName);
        if (canMoveToVoice1)
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Move to player 1",
                () => moveNotesToOtherVoiceAction.MoveNotesToVoiceAndNotify(songMeta, selectedNotes, Voice.firstVoiceName));
        }
        if (!canMoveToVoice1 && canMoveToVoice2)
        {
            contextMenu.AddSeparator();
        }
        if (canMoveToVoice2)
        {
            contextMenu.AddItem("Move to player 2",
                () => moveNotesToOtherVoiceAction.MoveNotesToVoiceAndNotify(songMeta, selectedNotes, Voice.secondVoiceName));
        }
    }

    private void FillContextMenuToMoveToOtherSentence(ContextMenu contextMenu, List<Note> selectedNotes)
    {
        bool canMoveToPreviousSentence = moveNoteToAdjacentSentenceAction.CanMoveToPreviousSentence(selectedNotes, uiNote.Note);
        bool canMoveToNextSentence = moveNoteToAdjacentSentenceAction.CanMoveToNextSentence(selectedNotes, uiNote.Note);
        if (canMoveToPreviousSentence)
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Move to previous sentence",
                () => moveNoteToAdjacentSentenceAction.MoveToPreviousSentenceAndNotify(uiNote.Note));
        }
        if (!canMoveToPreviousSentence && canMoveToNextSentence)
        {
            contextMenu.AddSeparator();
        }
        if (canMoveToNextSentence)
        {
            contextMenu.AddItem("Move to next sentence",
                () => moveNoteToAdjacentSentenceAction.MoveToNextSentenceAndNotify(uiNote.Note));
        }
    }
}
