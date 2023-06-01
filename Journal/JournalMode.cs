﻿using System;
using System.Collections.Generic;
using Journal.External;
using OWML.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Journal;

public class JournalMode : ShipLogMode
{
    public const string Name = "Journal";

    public ItemListWrapper ItemList;
    public JournalStore Store;
    
    private OWAudioSource _oneShotSource;
    private readonly AudioType _openSound = AudioType.DialogueEnter;
    private readonly AudioType _positiveSound = AudioType.DialogueAdvance;
    private readonly AudioType _negativeSound = AudioType.DialogueExit;

    private State _currentState;
    private bool _creatingNewEntry;
    private bool _pendingSave;
    private IEpicasAlbumAPI _epicasAlbumAPI;

    private Image _photo;
    private Text _questionMark;
    private List<CustomInputField> _entryInputs;
    private CustomInputField _firstDescInput;
    private Image _firstDescBorderLine;

    private readonly Color _selectionTextColor = new(0f, 0.2f, 0.3f);
    private readonly Color _editingTextColor = new(0.7f, 1f, 0.5f);
    private readonly Color _deletingTextColor = Color.red;
    private Color _prevTextColor;

    // TODO: Kbm (like desc field), gamepad with only text
    private ScreenPromptList _upperRightPromptList;
    private ScreenPromptList _mainPromptList;
    private CanvasGroupAnimator _mainPromptListAnimator;
    private readonly Vector3 _mainPromptListShownScale = Vector3.one * 1.5f; // Other prompts are with this scale (in root)...
    private readonly Vector3 _mainPromptListHiddenScale = new Vector3(0f, 1f, 1f) * 1.5f;
    private ScreenPrompt _createEntryPrompt;
    private ScreenPrompt _renameEntryPrompt;
    private ScreenPrompt _editDescriptionPrompt;
    private ScreenPrompt _setPhotoPrompt;
    private ScreenPrompt _removePhotoPrompt;
    private ScreenPrompt _toggleMoreToExplorePrompt;
    private ScreenPrompt _moveEntryPrompt;
    private ScreenPrompt _deleteEntryPrompt;
    private ScreenPrompt _togglePromptsPrompt;

    public enum State
    {
        Disabled,
        Main,
        Renaming,
        EditingDescription,
        Deleting,
        ChoosingPhoto
    }
    
    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _oneShotSource = oneShotSource;
        _upperRightPromptList = upperRightPromptList;

        ItemList.SetName(Name);
        _photo = ItemList.GetPhoto();
        _photo.preserveAspect = true; // Maybe this should be the default value...
        _questionMark = ItemList.GetQuestionMark();

        _entryInputs = new List<CustomInputField>();
        foreach (ShipLogEntryListItem entryListItem in ItemList.GetItemsUI())
        {
            Text text = entryListItem._nameField;
            CustomInputField input = AddInputFieldInput(text);
            // TODO: max length
            _entryInputs.Add(input);
        }
        
        ItemList.DescriptionFieldClear();
        Text firstDescText = ItemList.DescriptionFieldGetNextItem()._text;
        _firstDescInput = AddInputFieldInput(firstDescText);
        _firstDescInput.lineType = CustomInputField.LineType.MultiLineNewline;
        _firstDescBorderLine = _firstDescInput.transform.Find("EntryBorderLine").GetComponent<Image>();
        // TODO: idea: force expand height + not infinite panel (add to the mask thing?), sizedelta.y = 1 (for the last row... although active scrolling!)

        _epicasAlbumAPI = Journal.Instance.ModHelper.Interaction
            .TryGetModApi<IEpicasAlbumAPI>("dgarro.EpicasAlbum");
        
        SetupPrompts();

        _currentState = State.Disabled;
    }

    private void SetupPrompts()
    {
        // TODO: Translate
        // Not using the ScreenPrompt.MultiCommandType.HOLD_ONE_AND_PRESS_2ND, too much space...
        string holdPrompt = UITextLibrary.GetString(UITextType.HoldPrompt);
        _createEntryPrompt = new ScreenPrompt(InputLibrary.shiftL, InputLibrary.enter,
            $"Create Entry <CMD1>{holdPrompt} +<CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _renameEntryPrompt = new ScreenPrompt(InputLibrary.enter, $"Rename Entry <CMD>{holdPrompt}");
        _editDescriptionPrompt = new ScreenPrompt(InputLibrary.enter, "Edit Description");
        _setPhotoPrompt = new ScreenPrompt(InputLibrary.toolActionPrimary, "Change Photo"); // TODO: Or Add
        _removePhotoPrompt = new ScreenPrompt(InputLibrary.toolActionPrimary, $"Remove Photo <CMD>{holdPrompt}");
        _toggleMoreToExplorePrompt = new ScreenPrompt(InputLibrary.thrustDown, "Remove More to Explore"); // TODO: Or Add
        _moveEntryPrompt = new ScreenPrompt(InputLibrary.thrustUp, $"Move Entry <CMD>{holdPrompt}");
        _deleteEntryPrompt = new ScreenPrompt(InputLibrary.toolActionSecondary, "Delete Entry");
        
        _togglePromptsPrompt = new ScreenPrompt(InputLibrary.map, "Show Prompts"); // TODO: Or Hide
        
        GameObject promptListGo = new GameObject("PromptList", 
            typeof(VerticalLayoutGroup), typeof(ScreenPromptList), typeof(CanvasGroupAnimator));
        RectTransform promptListRect = promptListGo.GetComponent<RectTransform>();
        promptListRect.parent = transform;
        promptListRect.localPosition = Vector3.zero;
        promptListRect.localEulerAngles = Vector3.zero;
        promptListRect.localScale = Vector3.one * 1.5f;
        promptListRect.anchoredPosition = new Vector2(140, 225); // Hardcoded floating thing...
        promptListRect.sizeDelta = new Vector2(150, 200);
        promptListRect.pivot = new Vector2(1f, 1f); // important x for the animation from left
        _mainPromptList = promptListGo.GetComponent<ScreenPromptList>();
        VerticalLayoutGroup promptListLayoutGroup = promptListGo.GetComponent<VerticalLayoutGroup>();
        promptListLayoutGroup.childAlignment = TextAnchor.UpperRight;
        promptListLayoutGroup.spacing = 0;
        promptListLayoutGroup.childForceExpandWidth = false;
        promptListLayoutGroup.childForceExpandHeight = false;
        _mainPromptListAnimator = promptListGo.AddComponent<CanvasGroupAnimator>();
        _mainPromptListAnimator.SetImmediate(1f, Store.Data.ShowPrompts ? _mainPromptListShownScale : _mainPromptListHiddenScale);
    }

    private CustomInputField AddInputFieldInput(Text text)
    {
        CustomInputField input = text.gameObject.AddComponent<CustomInputField>();
        input.textComponent = text;
        input.caretWidth = 2; // Otherwise it's too thin
        input.selectionColor = _selectionTextColor; // Important alpha=1 for overlap in description field
        input.enabled = false;
        // input.onValueChanged.AddListener(_ => _oneShotSource.PlayOneShot(_positiveSound)); // Lower volume (NOT WORKING WHEN A LOT) 
        return input;
    }

    public bool UsingInput()
    {
        return _currentState is State.Renaming or State.EditingDescription;
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        OpenList();
        UpdateItems();
        UpdateDescriptionField();
        UpdatePhoto();

        if (_currentState != State.Disabled)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on enter!", MessageType.Error);
        }
        _currentState = State.Main;
    }

    private void OpenList()
    {
        _oneShotSource.PlayOneShot(_openSound, 3f); // Or TH_ProjectorActivate?
        ItemList.Open();

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_createEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_renameEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_editDescriptionPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_setPhotoPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_removePhotoPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_toggleMoreToExplorePrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_moveEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_deleteEntryPrompt, _mainPromptList, TextAnchor.MiddleRight);
        
        promptManager.AddScreenPrompt(_togglePromptsPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
    }

    private void UpdateItems()
    {
        List<Tuple<string,bool,bool,bool>> items = new();
        foreach (JournalStore.Entry entry in Store.Data.Entries)
        {
            items.Add(new Tuple<string, bool, bool, bool>(entry.Name, false, false, entry.HasMoreToExplore));
        }
        ItemList.SetItems(items);
    }
    
    private void UpdateDescriptionField()
    {
        // Important to clear before the if, for example if the only entry was deleted
        ItemList.DescriptionFieldClear();
        if (Store.Data.Entries.Count > 0)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
            string description = selectedEntry.Description;
            string[] facts = description.Split(new[] { "\n\n" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string fact in facts)
            {
                ShipLogFactListItem item = ItemList.DescriptionFieldGetNextItem();
                item.DisplayText(fact.TrimStart('\n'));
            }

            if (selectedEntry.HasMoreToExplore)
            {
                ShipLogFactListItem moreToExploreItem = ItemList.DescriptionFieldGetNextItem();
                // Like ShipLogEntryDescriptionField.SetEntry does...
                moreToExploreItem.DisplayText(UITextLibrary.GetString(UITextType.ShipLogMoreThere));
            }
        }
    }

    private void UpdatePhoto()
    {
        if (Store.Data.Entries.Count > 0)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
            if (selectedEntry.EpicasAlbumSnapshotName != null)
            {
                _questionMark.gameObject.SetActive(false);
                _photo.gameObject.SetActive(true);
                _photo.sprite = _epicasAlbumAPI.GetSnapshotSprite(selectedEntry.EpicasAlbumSnapshotName);
            }
            else
            {
                _questionMark.gameObject.SetActive(true);
                _photo.gameObject.SetActive(false);
            }
        }
        else
        {
            _questionMark.gameObject.SetActive(false);
            _photo.gameObject.SetActive(false);
        }
    }

    public override void ExitMode()
    {
        CloseList();
        // TODO: Save more often?
        if (_pendingSave)
        {
            Store.SaveToDisk();
            _pendingSave = false;
        }

        if (_currentState != State.Main)
        {
            Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on exit!", MessageType.Error);
        }
        _currentState = State.Disabled;
    }

    private void CloseList()
    {
        ItemList.Close();

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_createEntryPrompt);        
        promptManager.RemoveScreenPrompt(_renameEntryPrompt);
        promptManager.RemoveScreenPrompt(_editDescriptionPrompt);
        promptManager.RemoveScreenPrompt(_setPhotoPrompt);
        promptManager.RemoveScreenPrompt(_removePhotoPrompt);
        promptManager.RemoveScreenPrompt(_toggleMoreToExplorePrompt);
        promptManager.RemoveScreenPrompt(_moveEntryPrompt);
        promptManager.RemoveScreenPrompt(_deleteEntryPrompt);
        
        promptManager.RemoveScreenPrompt(_togglePromptsPrompt);
    }

    public override void UpdateMode()
    {
        // TODO: What happens if desc field is scrolled while editing???

        _createEntryPrompt.SetVisibility(true);
        // if (OWInput.UsingGamepad())
        // {
        //     _createEntryPrompt._multiCommandType = ScreenPrompt.MultiCommandType.CUSTOM_BOTH;
        //     _createEntryPrompt.SetText("<CMD1> <CMD2> Requires Keyboard");
        // }
        // else
        // {
        //     _createEntryPrompt._multiCommandType = ScreenPrompt.MultiCommandType.CUSTOM_BOTH;
        //     _createEntryPrompt.SetText("Create Entry <CMD1>{holdPrompt} +<CMD2>");
        // }

        _renameEntryPrompt.SetVisibility(true);
        _editDescriptionPrompt.SetVisibility(true);        
        _setPhotoPrompt.SetVisibility(true);
        _removePhotoPrompt.SetVisibility(true);
        _toggleMoreToExplorePrompt.SetVisibility(true);
        _moveEntryPrompt.SetVisibility(true);
        _deleteEntryPrompt.SetVisibility(true);
        
        _togglePromptsPrompt.SetVisibility(true);

        switch (_currentState)
        {
            case State.Main:
                int prevSelectedIndex = ItemList.GetSelectedIndex();
                int delta = ItemList.UpdateList();
                if (delta != 0)
                {
                    bool movingEntry = OWInput.IsPressed(InputLibrary.thrustUp);
                    if (movingEntry)
                    {
                        int newSelectedIndex = ItemList.GetSelectedIndex();
                        (Store.Data.Entries[prevSelectedIndex], Store.Data.Entries[newSelectedIndex]) =
                            (Store.Data.Entries[newSelectedIndex], Store.Data.Entries[prevSelectedIndex]);
                        UpdateItems();
                        ItemList.UpdateListUI(); // Avoid ugly frame, show the updated list now
                        _pendingSave = true;
                        _oneShotSource.PlayOneShot(delta > 0 ? _positiveSound : _negativeSound, 3f);
                        return; // Don't do any additional action when moving (also no need to change description or photo)
                    }

                    UpdateDescriptionField();
                    UpdatePhoto();
                }

                if (OWInput.IsNewlyPressed(InputLibrary.map))
                {
                    TogglePrompts();
                    return;
                }
                
                // Keyboard-required actions, all with enter
                bool shiftPressed = OWInput.IsPressed(InputLibrary.shiftL) || OWInput.IsPressed(InputLibrary.shiftR);
                if (shiftPressed && OWInput.IsNewlyPressed(InputLibrary.enter))
                {
                    CreateEntry();
                }
                if (Store.Data.Entries.Count == 0)
                {
                    return;
                }
                if (OWInput.IsPressed(InputLibrary.enter, 0.5f))
                {
                    RenameEntry();
                }
                else if (OWInput.IsNewlyReleased(InputLibrary.enter)) // Released because the user may want to hold it...
                {
                    EditDescription();
                }
                
                else if (OWInput.IsNewlyPressed(InputLibrary.thrustDown))
                {
                    ToggleMoreToExplore();
                }
                else if (OWInput.IsNewlyPressed(InputLibrary.toolActionPrimary))
                {
                    ChoosePhoto();
                }
                else if (OWInput.IsNewlyPressed(InputLibrary.toolActionSecondary)) // Like Épicas
                {
                    MarkForDeletion();
                }
                break;
            case State.Renaming:
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    RenameEntryEnd();
                }
                break;
            case State.EditingDescription:
                // TODO: PAUSE
                if (OWInput.IsNewlyPressed(InputLibrary.escape))
                {
                    EditDescriptionEnd();
                }
                break;
            case State.Deleting:
                if (OWInput.IsNewlyPressed(InputLibrary.cancel))
                {
                    UnmarkForDeletion();
                }
                else if (OWInput.IsPressed(InputLibrary.interact, 0.7f)) // enter would edit description next frame (because not Newly)
                {
                    DeleteEntry();
                }
                break;
            case State.ChoosingPhoto:
                // Do nothing, we aren't actually active right now (should we change mode?)
                break;
            default:
                Journal.Instance.ModHelper.Console.WriteLine($"Unexpected state {_currentState} on update!", MessageType.Error);
                break;
        }
    }

    private void CreateEntry()
    {
        List<JournalStore.Entry> entries = Store.Data.Entries;
        int newIndex = entries.Count == 0 ? 0 : ItemList.GetSelectedIndex() + 1;
        JournalStore.Entry newEntry = new JournalStore.Entry();
        entries.Insert(newIndex, newEntry);
        ItemList.SetSelectedIndex(newIndex);
        UpdateItems();
        UpdateDescriptionField();
        UpdatePhoto();
        ItemList.UpdateListUI(); // We want to update the UI but not move because of renaming
        _creatingNewEntry = true; // Only really necessary to remember to go to edit description next
        RenameEntry();
        // No need to save file, it would be saved on description end
    }
    
    private void RenameEntry()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        inputField.text = Store.Data.Entries[selectedIndex].Name;
        EnableInputField(inputField);
        _currentState = State.Renaming;
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }

    private void RenameEntryEnd()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        CustomInputField inputField = _entryInputs[ItemList.GetIndexUI(selectedIndex)];
        Store.Data.Entries[selectedIndex].Name = inputField.text;
        DisableInputField(inputField);
        UpdateItems();
        // This is just for an alpha or something in UI index 4 (text should already be the correct one),
        // noticeable for a frame or while editing description in entry
        ItemList.UpdateListUI(); 
        if (_creatingNewEntry)
        {
            // TODO: Consider this in the "confirm" prompt?
            // TODO: Update UI? Alpha for some reason, rumor color too?
            EditDescription();
            // No need to save file, it would be saved on description end
        }
        else
        {
            _currentState = State.Main;
            _pendingSave = true;
            _oneShotSource.PlayOneShot(_negativeSound, 3f);
        }
    }

    private void EditDescription()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        _firstDescInput.text = Store.Data.Entries[selectedIndex].Description;
        ItemList.DescriptionFieldClear();
        ItemList.DescriptionFieldGetNextItem();
        EnableInputField(_firstDescInput);
        _firstDescBorderLine.enabled = false;
        _currentState = State.EditingDescription;
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }

    private void EditDescriptionEnd()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Store.Data.Entries[selectedIndex].Description = _firstDescInput.text;
        DisableInputField(_firstDescInput);
        _firstDescBorderLine.enabled = true;
        UpdateDescriptionField();
        _currentState = State.Main;
        _creatingNewEntry = false;
        _pendingSave = true;
        _oneShotSource.PlayOneShot(_negativeSound, 3f);
    }

    private void EnableInputField(CustomInputField inputField)
    {
        inputField.onFocusSelectAll = _creatingNewEntry; // To quickly replace placeholder default text
        inputField.enabled = true;
        OWInput.ChangeInputMode(InputMode.KeyboardInput);
        Locator.GetPauseCommandListener().AddPauseCommandLock();
        inputField.ActivateInputField();
        _prevTextColor = inputField.textComponent.color;
        inputField.textComponent.color = _editingTextColor;
    }

    private void DisableInputField(CustomInputField inputField)
    {
        inputField.textComponent.color = _prevTextColor;
        inputField.DeactivateInputField();
        Locator.GetPauseCommandListener().RemovePauseCommandLock();
        OWInput.RestorePreviousInputs();
        inputField.enabled = false;
    }
    
    private void ToggleMoreToExplore()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Store.Data.Entries[selectedIndex].HasMoreToExplore = !Store.Data.Entries[selectedIndex].HasMoreToExplore;
        UpdateItems();
        UpdateDescriptionField(); // Remember that it has an item for more to explore
        ItemList.UpdateListUI(); // To match the icon with the description already changed in this frame
        _pendingSave = true;
        _oneShotSource.PlayOneShot(Store.Data.Entries[selectedIndex].HasMoreToExplore ? _positiveSound : _negativeSound, 3f);
    }

    private void ChoosePhoto()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        JournalStore.Entry selectedEntry = Store.Data.Entries[selectedIndex];
        _epicasAlbumAPI.OpenSnapshotChooserDialog(selectedEntry.EpicasAlbumSnapshotName, ChoosePhotoEnd);
        _currentState = State.ChoosingPhoto;
        CloseList();
    }

    private void ChoosePhotoEnd(string selectedSnapshotName)
    {
        if (selectedSnapshotName != null)
        {
            int selectedIndex = ItemList.GetSelectedIndex();
            Store.Data.Entries[selectedIndex].EpicasAlbumSnapshotName = selectedSnapshotName;
            UpdatePhoto();
            _pendingSave = true;
        }
        // Wait a frame to avoid closing the mode on UpdateMode() if run after this on same frame
        Journal.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
        {
            _currentState = State.Main;
            OpenList();
        });
    }

    private void MarkForDeletion()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Text text = ItemList.GetItemsUI()[ItemList.GetIndexUI(selectedIndex)]._nameField;
        _prevTextColor = text.color;
        // Maybe I could use rich text in UpdateItems()? Like I would do for rumored I guess...
        text.color = _deletingTextColor;
        _currentState = State.Deleting;
        _oneShotSource.PlayOneShot(_positiveSound, 3f);
    }
    
    private void UnmarkForDeletion()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        Text text = ItemList.GetItemsUI()[ItemList.GetIndexUI(selectedIndex)]._nameField;
        text.color = _prevTextColor;
        _currentState = State.Main;
        _oneShotSource.PlayOneShot(_negativeSound, 3f);
    }

    private void DeleteEntry()
    {
        int selectedIndex = ItemList.GetSelectedIndex();
        List<JournalStore.Entry> entries = Store.Data.Entries;
        entries.RemoveAt(selectedIndex);
        UnmarkForDeletion(); // Important to do before changing index to restore color!
        if (selectedIndex >= entries.Count && entries.Count > 0)
        {
            // Same check as Épicas, idk if the -1 is bad but just in case...
            ItemList.SetSelectedIndex(selectedIndex - 1);
        }
        UpdateItems();
        UpdateDescriptionField();
        UpdatePhoto();
        ItemList.UpdateListUI(); // To match the selected entry with the description already changed in this frame
        _pendingSave = true;
    }

    private void TogglePrompts()
    {
        Store.Data.ShowPrompts = !Store.Data.ShowPrompts;
        _mainPromptListAnimator.AnimateTo(1f, 
                Store.Data.ShowPrompts? _mainPromptListShownScale : _mainPromptListHiddenScale, 0.15f);
        _oneShotSource.PlayOneShot(Store.Data.ShowPrompts ? _positiveSound : _negativeSound);
        _pendingSave = true;
    }

    public override bool AllowModeSwap()
    {
        return _currentState == State.Main;
    }

    public override bool AllowCancelInput()
    {
        // TODO: fix _exitPrompt not changing!
        return _currentState == State.Main;
    }

    public override void OnEnterComputer()
    {
        // No-op
    }

    public override void OnExitComputer()
    {
        // No-op
    }    

    public override string GetFocusedEntryID()
    {
        return "";
    }
}
